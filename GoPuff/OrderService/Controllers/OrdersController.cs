using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.DTOs;
using Shared.Data;
using Shared.Models;
using Shared.Utils;

namespace OrderService.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly GoPuffDbContext _db;
    private readonly NearbyClient _nearby;
    private readonly InventoryCacheService _cache;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        GoPuffDbContext db,
        NearbyClient nearby,
        InventoryCacheService cache,
        ILogger<OrdersController> logger)
    {
        _db = db;
        _nearby = nearby;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Places an order.
    ///
    /// Consistency guarantee: inventory is decremented via a conditional UPDATE
    ///   UPDATE inventories SET quantity = quantity - N WHERE item_id = ? AND fc_id = ? AND quantity >= N
    /// This is atomic at the row level in PostgreSQL — two concurrent requests for the
    /// last unit cannot both succeed. No explicit row locks are needed.
    ///
    /// The entire order is wrapped in a transaction so a failure on any line item rolls
    /// back all previously claimed inventory in the same request.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request)
    {
        _logger.LogInformation(
            "PlaceOrder user={User}, items={Count}", request.UserId, request.Items.Count);

        // 1. Resolve nearby FCs
        var fcIds = await _nearby.GetNearbyFcIdsAsync(
            request.DeliveryLat, request.DeliveryLon, request.RadiusMiles);

        if (fcIds.Count == 0)
            return UnprocessableEntity("No fulfilment centres within the delivery radius.");

        // 2. Pre-load FC and item metadata for the response
        var fcsMap = await _db.FulfillmentCentres.Where(f => fcIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id);
        var itemIds = request.Items.Select(i => i.ItemId).Distinct().ToList();
        var itemsMap = await _db.Items.Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);

        var fulfilledLines = new List<FulfilledLine>();
        var cacheInvalidations = new List<(int itemId, int fcId)>();
        int orderId;

        // 3. Claim inventory inside a single transaction
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var line in request.Items)
            {
                if (!itemsMap.TryGetValue(line.ItemId, out var item))
                    return NotFound($"Item {line.ItemId} does not exist.");

                var claimed = false;

                foreach (var fcId in fcIds)
                {
                    // Atomic conditional decrement — only proceeds if stock is sufficient.
                    // The WHERE quantity >= N prevents negative inventory without locks.
                    int rowsAffected = await _db.Database.ExecuteSqlRawAsync(
                        "UPDATE inventories SET quantity = quantity - {0} " +
                        "WHERE item_id = {1} AND fc_id = {2} AND quantity >= {0}",
                        line.Quantity, line.ItemId, fcId);

                    if (rowsAffected > 0)
                    {
                        var fcName = fcsMap.TryGetValue(fcId, out var fc) ? fc.Name : fcId.ToString();
                        fulfilledLines.Add(new FulfilledLine(line.ItemId, item.Name, fcId, fcName, line.Quantity));
                        cacheInvalidations.Add((line.ItemId, fcId));
                        claimed = true;
                        break;
                    }
                }

                if (!claimed)
                {
                    await tx.RollbackAsync();
                    _logger.LogWarning("Out of stock: item={ItemId}", line.ItemId);
                    return Conflict($"Item '{item.Name}' (id={line.ItemId}) is out of stock near you.");
                }
            }

            // 4. Persist the order record
            var order = new Order
            {
                UserId = request.UserId,
                DeliveryLat = request.DeliveryLat,
                DeliveryLon = request.DeliveryLon,
                CreatedAt = DateTime.UtcNow,
                Items = fulfilledLines.Select(l => new OrderItem
                {
                    ItemId = l.ItemId,
                    FcId = l.FcId,
                    Quantity = l.Quantity
                }).ToList()
            };

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            orderId = order.Id;
            _logger.LogInformation("Order {OrderId} committed for user {User}", orderId, request.UserId);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Unexpected error placing order");
            return StatusCode(500, "An unexpected error occurred while placing the order.");
        }

        // 5. Eagerly invalidate inventory cache so the next availability read is fresh
        foreach (var (itemId, fcId) in cacheInvalidations)
            await _cache.InvalidateAsync(itemId, fcId);

        return Ok(new PlaceOrderResponse(orderId, request.UserId, fulfilledLines));
    }
}

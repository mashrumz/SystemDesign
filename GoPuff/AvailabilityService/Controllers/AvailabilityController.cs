using AvailabilityService.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Utils;

namespace AvailabilityService.Controllers;

[ApiController]
[Route("availability")]
public class AvailabilityController : ControllerBase
{
    private readonly GoPuffDbContext _db;
    private readonly NearbyClient _nearby;
    private readonly InventoryCacheService _cache;
    private readonly ILogger<AvailabilityController> _logger;

    public AvailabilityController(
        GoPuffDbContext db,
        NearbyClient nearby,
        InventoryCacheService cache,
        ILogger<AvailabilityController> logger)
    {
        _db = db;
        _nearby = nearby;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Returns stock availability for the requested items at the given delivery location.
    /// The effective stock for each item is the sum across all nearby FCs (union of stock).
    ///
    /// Flow:
    ///   1. Ask NearbyService for FC IDs within the delivery radius (NearbyService owns the FC cache).
    ///   2. For each (itemId, fcId) pair check the Inventory Cache (Redis, TTL 30 s).
    ///   3. Batch-Query the DB for all cache misses and back-fill the cache.
    ///   4. Return aggregated totals.
    /// </summary>
    /// <param name="lat">Delivery latitude</param>
    /// <param name="lon">Delivery longitude</param>
    /// <param name="itemIds">Comma-separated item IDs, e.g. 1,2,3</param>
    /// <param name="radiusMiles">Search radius in miles (default 30)</param>
    [HttpGet]
    public async Task<IActionResult> GetAvailability(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] string itemIds,
        [FromQuery] double radiusMiles = 30)
    {
        var ids = itemIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : -1)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return BadRequest("itemIds must be a non-empty comma-separated list of valid integers.");

        _logger.LogInformation(
            "Availability check lat={Lat}, lon={Lon}, items={Items}", lat, lon, string.Join(",", ids));

        // Step 1: resolve nearby FCs via NearbyService (it owns the FC cache)
        var fcIds = await _nearby.GetNearbyFcIdsAsync(lat, lon, radiusMiles);
        if (fcIds.Count == 0)
            return Ok(new AvailabilityResponse([], []));

        // Step 2: check inventory cache for each (itemId, fcId) pair
        var totals = new Dictionary<int, int>();
        var misses = new List<(int itemId, int fcId)>();

        foreach (var itemId in ids)
        {
            totals[itemId] = 0;
            foreach (var fcId in fcIds)
            {
                var qty = await _cache.GetQuantityAsync(itemId, fcId);
                if (qty.HasValue)
                    totals[itemId] += qty.Value;
                else
                    misses.Add((itemId, fcId));
            }
        }

        // Step 3: batch DB query for all cache misses
        if (misses.Count > 0)
        {
            var missItemIds = misses.Select(m => m.itemId).Distinct().ToList();
            var missFcIds = misses.Select(m => m.fcId).Distinct().ToList();

            var dbRows = await _db.Inventories
                .Where(i => missItemIds.Contains(i.ItemId) && missFcIds.Contains(i.FcId))
                .Select(i => new { i.ItemId, i.FcId, i.Quantity })
                .ToListAsync();

            var dbMap = dbRows.ToDictionary(r => (r.ItemId, r.FcId), r => r.Quantity);

            foreach (var (itemId, fcId) in misses)
            {
                var qty = dbMap.TryGetValue((itemId, fcId), out var q) ? q : 0;
                await _cache.SetQuantityAsync(itemId, fcId, qty);
                totals[itemId] += qty;
            }
        }

        // Step 4: enrich with item names
        var itemMap = await _db.Items
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name);

        var result = ids.Select(id => new ItemAvailability(
            id,
            itemMap.TryGetValue(id, out var name) ? name : "Unknown",
            totals.GetValueOrDefault(id, 0),
            totals.GetValueOrDefault(id, 0) > 0
        )).ToList();

        return Ok(new AvailabilityResponse(result, fcIds));
    }
}

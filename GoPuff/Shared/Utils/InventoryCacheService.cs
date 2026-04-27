using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Shared.Utils;

/// <summary>
/// Cache-aside layer for per-(itemId, fcId) inventory quantities.
/// TTL: 30 seconds — short enough to keep counts fresh, long enough to absorb
/// read bursts on the availability path.
/// OrderService calls InvalidateAsync after a successful order to eagerly
/// evict the stale count so the next availability read goes to the DB.
/// Redis instance is keyed "inv-cache" in DI.
/// </summary>
public class InventoryCacheService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private readonly IDatabase _db;
    private readonly ILogger<InventoryCacheService> _logger;

    public InventoryCacheService(
        [FromKeyedServices("inv-cache")] IConnectionMultiplexer redis,
        ILogger<InventoryCacheService> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    /// <summary>Returns the cached quantity, or null on a cache miss.</summary>
    public async Task<int?> GetQuantityAsync(int itemId, int fcId)
    {
        var value = await _db.StringGetAsync(BuildKey(itemId, fcId));
        if (value.IsNullOrEmpty) return null;
        return (int)value;
    }

    /// <summary>Stores a quantity with the standard 30 s TTL.</summary>
    public async Task SetQuantityAsync(int itemId, int fcId, int quantity)
        => await _db.StringSetAsync(BuildKey(itemId, fcId), quantity, Ttl);

    /// <summary>Eagerly removes a key so the next read hits the DB.</summary>
    public async Task InvalidateAsync(int itemId, int fcId)
    {
        await _db.KeyDeleteAsync(BuildKey(itemId, fcId));
        _logger.LogDebug("Inventory cache invalidated item={ItemId} fc={FcId}", itemId, fcId);
    }

    private static string BuildKey(int itemId, int fcId) => $"inv:{itemId}:{fcId}";
}

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Shared.Utils;

/// <summary>
/// Caches the full list of all Fulfilment Centres (id, lat, lon) in Redis.
/// NearbyService loads all FCs into this cache on startup; on a cache miss the
/// caller must reload from the DB and re-populate.
/// TTL is 5 minutes — FCs almost never move, so a little staleness is fine.
/// Redis instance is keyed "fc-cache" in DI.
/// </summary>
public class FcCacheService
{
    private const string AllFcsKey = "fcs:all";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly IDatabase _db;
    private readonly ILogger<FcCacheService> _logger;

    public FcCacheService(
        [FromKeyedServices("fc-cache")] IConnectionMultiplexer redis,
        ILogger<FcCacheService> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    /// <summary>Returns all cached FCs, or null on a cache miss.</summary>
    public async Task<List<FcEntry>?> GetAllAsync()
    {
        var value = await _db.StringGetAsync(AllFcsKey);
        if (value.IsNullOrEmpty) return null;

        _logger.LogDebug("FC cache hit — returning full FC list");
        return JsonSerializer.Deserialize<List<FcEntry>>((string)value!);
    }

    /// <summary>Stores the full FC list with a 5-minute TTL.</summary>
    public async Task SetAllAsync(List<FcEntry> fcs)
    {
        await _db.StringSetAsync(AllFcsKey, JsonSerializer.Serialize(fcs), Ttl);
        _logger.LogDebug("FC cache populated with {Count} FCs", fcs.Count);
    }

    /// <summary>Removes the FC list from cache (e.g., after adding/removing an FC).</summary>
    public async Task InvalidateAsync() => await _db.KeyDeleteAsync(AllFcsKey);
}

/// <summary>Lightweight FC projection stored in the FC cache.</summary>
public record FcEntry(int Id, string Name, double Lat, double Lon);

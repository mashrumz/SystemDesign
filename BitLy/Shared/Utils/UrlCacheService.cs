using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Shared.Utils;

/// <summary>
/// Redis-backed URL cache (separate instance from the global counter).
/// Eviction is handled server-side via the allkeys-lru policy configured on the cache Redis instance.
/// TTL is applied only when the short URL has an explicit expiration date.
/// </summary>
public class UrlCacheService
{
    private readonly IDatabase _db;
    private readonly ILogger<UrlCacheService> _logger;

    public UrlCacheService(
        [FromKeyedServices("cache")] IConnectionMultiplexer cache,
        ILogger<UrlCacheService> logger)
    {
        _db = cache.GetDatabase();
        _logger = logger;
    }

    public async Task<CachedUrl?> GetAsync(string shortCode)
    {
        var value = await _db.StringGetAsync(shortCode);
        if (value.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<CachedUrl>(value!);
    }

    public async Task SetAsync(string shortCode, string longUrl, DateTime? expirationDate)
    {
        TimeSpan? ttl = null;

        if (expirationDate.HasValue)
        {
            var remaining = expirationDate.Value.ToUniversalTime() - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                _logger.LogInformation("Skipping cache write for {ShortCode}: already expired", shortCode);
                return;
            }
            ttl = remaining;
        }

        var entry = new CachedUrl { LongUrl = longUrl, ExpirationDate = expirationDate };
        await _db.StringSetAsync(shortCode, JsonSerializer.Serialize(entry), ttl);
        _logger.LogDebug("Cached shortCode={ShortCode}, ttl={Ttl}", shortCode, ttl?.ToString() ?? "no-expiry");
    }

    public async Task RemoveAsync(string shortCode)
    {
        await _db.KeyDeleteAsync(shortCode);
    }
}

public record CachedUrl
{
    public string LongUrl { get; init; } = string.Empty;
    public DateTime? ExpirationDate { get; init; }
}

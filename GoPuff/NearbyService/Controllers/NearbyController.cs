using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Utils;

namespace NearbyService.Controllers;

[ApiController]
[Route("nearby")]
public class NearbyController : ControllerBase
{
    private readonly GoPuffDbContext _db;
    private readonly FcCacheService _cache;
    private readonly ILogger<NearbyController> _logger;

    public NearbyController(
        GoPuffDbContext db,
        FcCacheService cache,
        ILogger<NearbyController> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Returns IDs of Fulfilment Centres within <paramref name="radiusMiles"/> of the
    /// delivery point, using the Haversine formula computed in memory.
    ///
    /// Cache strategy: the full FC list is stored in Redis on startup (TTL 5 min).
    /// On a cache miss the list is reloaded from the DB and the cache is re-populated.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetNearby(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] double radiusMiles = 30)
    {
        _logger.LogInformation(
            "GetNearby lat={Lat}, lon={Lon}, radius={Radius}mi", lat, lon, radiusMiles);

        // 1. Try FC cache
        var allFcs = await _cache.GetAllAsync();

        if (allFcs is null)
        {
            // 2. Cache miss — load from DB and repopulate cache
            _logger.LogInformation("FC cache miss — loading from DB");
            allFcs = await _db.FulfillmentCentres
                .Select(f => new FcEntry(f.Id, f.Name, f.Lat, f.Lon))
                .ToListAsync();
            await _cache.SetAllAsync(allFcs);
        }

        // 3. Haversine filter in-memory
        var nearbyIds = allFcs
            .Where(fc => Haversine.DistanceMiles(lat, lon, fc.Lat, fc.Lon) <= radiusMiles)
            .Select(fc => fc.Id)
            .ToList();

        _logger.LogInformation(
            "Found {Count}/{Total} FCs within {Radius}mi", nearbyIds.Count, allFcs.Count, radiusMiles);

        return Ok(new { fcIds = nearbyIds });
    }
}

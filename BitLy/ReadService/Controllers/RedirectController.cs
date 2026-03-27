using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Utils;

namespace ReadService.Controllers;

[ApiController]
[Route("{shortCode}")]
public class RedirectController : ControllerBase
{
    private readonly UrlShortenerDbContext _db;
    private readonly UrlCacheService _cache;
    private readonly ILogger<RedirectController> _logger;

    public RedirectController(
        UrlShortenerDbContext db,
        UrlCacheService cache,
        ILogger<RedirectController> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetByShortCode(string shortCode)
    {
        _logger.LogInformation("Lookup for shortCode={ShortCode}", shortCode);

        // --- Cache-aside: check Redis first ---
        var cached = await _cache.GetAsync(shortCode);
        if (cached != null)
        {
            if (cached.ExpirationDate.HasValue && cached.ExpirationDate.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("shortCode expired (cache): {ShortCode}", shortCode);
                await _cache.RemoveAsync(shortCode);
                return StatusCode(410, "Gone");
            }
            _logger.LogInformation("Cache hit: redirecting {ShortCode} to {LongUrl}", shortCode, cached.LongUrl);
            return base.Redirect(cached.LongUrl);
        }

        // --- Cache miss: fall back to DB ---
        _logger.LogInformation("Cache miss for {ShortCode}, querying DB", shortCode);
        var shortUrl = await _db.ShortUrls.FirstOrDefaultAsync(s => s.ShortCode == shortCode);
        if (shortUrl == null)
        {
            _logger.LogWarning("shortCode not found: {ShortCode}", shortCode);
            return NotFound();
        }

        if (shortUrl.ExpirationDate.HasValue && shortUrl.ExpirationDate.Value < DateTime.UtcNow)
        {
            _logger.LogWarning("shortCode expired (DB): {ShortCode}, expiration={Expiration}", shortCode, shortUrl.ExpirationDate);
            return StatusCode(410, "Gone");
        }

        // Populate cache for future reads.
        await _cache.SetAsync(shortCode, shortUrl.LongUrl, shortUrl.ExpirationDate);

        _logger.LogInformation("Redirecting shortCode={ShortCode} to {LongUrl}", shortCode, shortUrl.LongUrl);
        return base.Redirect(shortUrl.LongUrl);
    }
}
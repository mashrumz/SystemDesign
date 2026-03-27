using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Models;
using Shared.Utils;
using WriteService.DTOs;

namespace WriteService.Controllers;

[ApiController]
[Route("urls")]
public class UrlsController : ControllerBase
{
    private readonly UrlShortenerDbContext _db;
    private readonly RedisCounter _counter;
    private readonly UrlCacheService _cache;
    private readonly ILogger<UrlsController> _logger;

    public UrlsController(
        UrlShortenerDbContext db,
        RedisCounter counter,
        UrlCacheService cache,
        ILogger<UrlsController> logger)
    {
        _db = db;
        _counter = counter;
        _cache = cache;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> ShortenUrl(ShortenUrlRequest request)
    {
        _logger.LogInformation("ShortenUrl request received for longUrl={LongUrl}, customAlias={CustomAlias}", request.LongUrl, request.CustomAlias);

        string shortCode;
        if (!string.IsNullOrEmpty(request.CustomAlias))
        {
            if (await _db.ShortUrls.AnyAsync(s => s.CustomAlias == request.CustomAlias))
            {
                _logger.LogWarning("Custom alias already exists: {CustomAlias}", request.CustomAlias);
                return BadRequest("Custom alias already exists");
            }
            shortCode = request.CustomAlias;
        }
        else
        {
            var id = await _counter.GetNextIdAsync();
            shortCode = Base62Encoder.Encode(id);
            _logger.LogInformation("Generated shortCode={ShortCode} for id={Id}", shortCode, id);
        }

        var shortUrl = new ShortUrl
        {
            LongUrl = request.LongUrl,
            ShortCode = shortCode,
            CustomAlias = request.CustomAlias,
            ExpirationDate = request.ExpirationDate
        };

        _db.ShortUrls.Add(shortUrl);
        await _db.SaveChangesAsync();

        // Write-through: populate the cache immediately so the first read is a cache hit.
        await _cache.SetAsync(shortCode, request.LongUrl, request.ExpirationDate);

        // Build URL dynamically to avoid manual environment setup for BASE_URL.
        // If the service is behind TLS terminator or proxy, use X-Forwarded-* headers.
        var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
        var host = Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? Request.Host.Value;
        var baseUrl = !string.IsNullOrEmpty(host) ? $"{scheme}://{host}" : (Environment.GetEnvironmentVariable("BASE_URL") ?? "https://short.ly");

        var resultUrl = $"{baseUrl}/{shortCode}";
        _logger.LogInformation("Short URL generated {ShortUrl} for longUrl={LongUrl}", resultUrl, request.LongUrl);

        return Ok(new ShortenUrlResponse { ShortUrl = resultUrl });
    }
}
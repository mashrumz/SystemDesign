using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Data;

namespace ReadService.Controllers;

[ApiController]
[Route("{shortCode}")]
public class RedirectController : ControllerBase
{
    private readonly UrlShortenerDbContext _db;
    private readonly ILogger<RedirectController> _logger;

    public RedirectController(UrlShortenerDbContext db, ILogger<RedirectController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetByShortCode(string shortCode)
    {
        _logger.LogInformation("Lookup for shortCode={ShortCode}", shortCode);

        var shortUrl = await _db.ShortUrls.FirstOrDefaultAsync(s => s.ShortCode == shortCode);
        if (shortUrl == null)
        {
            _logger.LogWarning("shortCode not found: {ShortCode}", shortCode);
            return NotFound();
        }

        if (shortUrl.ExpirationDate.HasValue && shortUrl.ExpirationDate.Value < DateTime.UtcNow)
        {
            _logger.LogWarning("shortCode expired: {ShortCode}, expiration={Expiration}", shortCode, shortUrl.ExpirationDate);
            return StatusCode(410, "Gone");
        }

        _logger.LogInformation("Redirecting shortCode={ShortCode} to {LongUrl}", shortCode, shortUrl.LongUrl);
        return base.Redirect(shortUrl.LongUrl);
    }
}
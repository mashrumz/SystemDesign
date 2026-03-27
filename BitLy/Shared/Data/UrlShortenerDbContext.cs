using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace Shared.Data;

public class UrlShortenerDbContext : DbContext
{
    public UrlShortenerDbContext(DbContextOptions<UrlShortenerDbContext> options)
        : base(options)
    {
    }

    public DbSet<ShortUrl> ShortUrls { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShortUrl>()
            .HasIndex(s => s.ShortCode)
            .IsUnique();

        modelBuilder.Entity<ShortUrl>()
            .HasIndex(s => s.CustomAlias)
            .IsUnique()
            .HasFilter("\"CustomAlias\" IS NOT NULL");
    }
}
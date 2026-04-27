using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Utils;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<GoPuffDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// FC Cache — separate Redis instance, stores the full FC list for in-memory Haversine
builder.Services.AddKeyedSingleton<IConnectionMultiplexer>("fc-cache",
    ConnectionMultiplexer.Connect(
        builder.Configuration["Redis:FcCacheConnectionString"] ?? "localhost:6379"));
builder.Services.AddScoped<FcCacheService>();

var app = builder.Build();

// ── DB warm-up: ensure schema exists, seed reference data, warm FC cache ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GoPuffDbContext>();
    var fcCache = scope.ServiceProvider.GetRequiredService<FcCacheService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    for (int attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            db.Database.EnsureCreated();
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning("DB not ready (attempt {A}/10): {M}", attempt, ex.Message);
            Thread.Sleep(3000);
        }
    }

    await DbSeeder.SeedAsync(db);

    // Pre-warm the FC cache so the first request doesn't round-trip to the DB
    var allFcs = await db.FulfillmentCentres
        .Select(f => new FcEntry(f.Id, f.Name, f.Lat, f.Lon))
        .ToListAsync();
    await fcCache.SetAllAsync(allFcs);
    logger.LogInformation("FC cache warmed with {Count} FCs", allFcs.Count);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();

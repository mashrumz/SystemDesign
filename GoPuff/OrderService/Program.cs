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

// Inventory Cache Redis — same logical instance as AvailabilityService; keyed "inv-cache"
builder.Services.AddKeyedSingleton<IConnectionMultiplexer>("inv-cache",
    ConnectionMultiplexer.Connect(
        builder.Configuration["Redis:InvCacheConnectionString"] ?? "localhost:6380"));
builder.Services.AddScoped<InventoryCacheService>();

// NearbyService typed HTTP client
var nearbyBaseUrl = builder.Configuration["Services:NearbyServiceUrl"] ?? "http://localhost:8082";
builder.Services.AddHttpClient<NearbyClient>(c => c.BaseAddress = new Uri(nearbyBaseUrl));

var app = builder.Build();

// ── DB warm-up ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GoPuffDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    for (int attempt = 1; attempt <= 10; attempt++)
    {
        try { db.Database.EnsureCreated(); break; }
        catch (Exception ex)
        {
            logger.LogWarning("DB not ready (attempt {A}/10): {M}", attempt, ex.Message);
            Thread.Sleep(3000);
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();

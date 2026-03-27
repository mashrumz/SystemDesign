using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Utils;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddDbContext<UrlShortenerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// URL look-up cache Redis instance (separate from the write-service counter Redis).
builder.Services.AddKeyedSingleton<IConnectionMultiplexer>("cache",
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:CacheConnectionString"] ?? "localhost:6380"));
builder.Services.AddScoped<UrlCacheService>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Auto-create tables on startup, with retries to wait for Postgres to be ready
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UrlShortenerDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    for (int i = 0; i < 10; i++)
    {
        try
        {
            db.Database.EnsureCreated();
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Database not ready yet (attempt {Attempt}/10): {Message}", i + 1, ex.Message);
            Thread.Sleep(3000);
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var useHttpsRedirect = app.Configuration.GetValue<bool?>("USE_HTTPS_REDIRECT") ?? false;
if (useHttpsRedirect)
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
app.MapControllers();
app.Run();
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Shared.Utils;

public class RedisCounter
{
    private readonly IDatabase _db;
    private const string CounterKey = "short_url_counter";

    public RedisCounter([FromKeyedServices("counter")] IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<long> GetNextIdAsync()
    {
        return await _db.StringIncrementAsync(CounterKey);
    }
}
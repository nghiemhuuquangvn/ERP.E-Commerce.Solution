using StackExchange.Redis;
using System.Text.Json;

namespace ERP.PO.Application.Caching;

public class RedisCacheService : ICacheService
{
    private readonly IDatabase _database;
    private readonly IServer _server;
    private readonly string _instanceName;

    public RedisCacheService(IConnectionMultiplexer connectionMultiplexer, string instanceName = "po_service")
    {
        _database = connectionMultiplexer.GetDatabase();
        _server = connectionMultiplexer.GetServer(connectionMultiplexer.GetEndPoints().First());
        _instanceName = instanceName;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var fullKey = $"{_instanceName}:{key}";
        var value = await _database.StringGetAsync(fullKey);

        if (!value.HasValue)
            return default;

        return JsonSerializer.Deserialize<T>(value.ToString());
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var fullKey = $"{_instanceName}:{key}";
        var serializedValue = JsonSerializer.Serialize(value);

        var ttl = expiration ?? TimeSpan.FromHours(1);

        await _database.StringSetAsync(fullKey, serializedValue, ttl);
    }

    public async Task RemoveAsync(string key)
    {
        var fullKey = $"{_instanceName}:{key}";
        await _database.KeyDeleteAsync(fullKey);
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        var searchPattern = $"{_instanceName}:{pattern}*";
        var keys = _server.Keys(pattern: searchPattern).ToArray();

        if (keys.Length > 0)
        {
            await _database.KeyDeleteAsync(keys);
        }
    }
}

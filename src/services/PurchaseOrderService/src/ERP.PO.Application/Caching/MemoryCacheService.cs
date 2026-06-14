using Microsoft.Extensions.Caching.Memory;
using System.Reflection;

namespace ERP.PO.Application.Caching;

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private static readonly Dictionary<string, byte> _cacheKeys = new();
    private static readonly object _lock = new();

    public MemoryCacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        return await Task.FromResult(_memoryCache.TryGetValue(key, out T? value) ? value : default);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var cacheOptions = new MemoryCacheEntryOptions();

        if (expiration.HasValue)
        {
            cacheOptions.AbsoluteExpirationRelativeToNow = expiration;
        }
        else
        {
            cacheOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
        }

        lock (_lock)
        {
            _cacheKeys[key] = 0;
        }

        _memoryCache.Set(key, value, cacheOptions);

        await Task.CompletedTask;
    }

    public async Task RemoveAsync(string key)
    {
        lock (_lock)
        {
            _cacheKeys.Remove(key);
        }

        _memoryCache.Remove(key);

        await Task.CompletedTask;
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        lock (_lock)
        {
            var keysToRemove = _cacheKeys.Keys.Where(k => k.Contains(pattern)).ToList();
            foreach (var key in keysToRemove)
            {
                _cacheKeys.Remove(key);
                _memoryCache.Remove(key);
            }
        }

        await Task.CompletedTask;
    }
}

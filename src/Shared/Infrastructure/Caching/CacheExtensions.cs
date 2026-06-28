using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Shared.Infrastructure.Caching;

public static class CacheExtensions
{
    /// <summary>
    /// Registers ICacheService. Uses Redis when "Cache:Type" = "Redis", otherwise in-memory.
    /// appsettings.json:
    /// {
    ///   "Cache": { "Type": "Redis", "InstanceName": "service_name" },
    ///   "ConnectionStrings": { "Redis": "localhost:6379" }
    /// }
    /// </summary>
    public static IServiceCollection AddCacheService(this IServiceCollection services, IConfiguration configuration, string defaultInstanceName = "erp")
    {
        var cacheType = configuration["Cache:Type"] ?? "Memory";
        var instanceName = configuration["Cache:InstanceName"] ?? defaultInstanceName;

        if (cacheType.Equals("Redis", StringComparison.OrdinalIgnoreCase))
        {
            var redisConnection = configuration.GetConnectionString("Redis")
                ?? throw new InvalidOperationException("Redis connection string 'Redis' is required when Cache:Type=Redis");

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = instanceName + ":";
            });
            services.AddSingleton<ICacheService, RedisCacheService>();
        }
        else
        {
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, MemoryCacheService>();
        }

        return services;
    }
}

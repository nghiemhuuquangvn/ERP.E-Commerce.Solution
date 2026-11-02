using ERP.Shared.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Shared.Infrastructure.Caching;

/// <summary>
/// <see cref="ICacheService"/> for tenant-owned data: every key gets the current tenant in front (<c>t:{tenant}:{key}</c>), so one shop can never
/// be served another shop's entry (R84 / R86: <c>crm:contacts:page:1</c> and <c>wh:barcode:{code}</c> were global).
/// <list type="bullet">
/// <item><b>Fail closed:</b> without a tenant (<see cref="ITenantProvider.CurrentTenantId"/> empty) nothing is read, written or removed through a
/// shared key; <c>GetOrSetAsync</c> just runs the factory.</item>
/// <item>Keys that start with one of the <i>platform-wide prefixes</i> (default <c>settings:</c>) are passed through untouched: the <c>…:system</c>
/// value is one for all shops and is invalidated by the platform, and tenant settings keys already contain the tenant id.</item>
/// <item>The tenant is read the way every DbContext reads it (<c>TenantScope</c> → JWT → gateway host header), so a consumer or a job inside a
/// <c>TenantScope</c> evicts the same entries its handlers wrote.</item>
/// </list>
/// Scoped (it reads the scoped <see cref="ITenantProvider"/>); register it with <see cref="TenantKeyedCacheExtensions.AddTenantKeyedCache"/>.
/// </summary>
public sealed class TenantKeyedCacheService : ICacheService
{
    private readonly ICacheService _inner;
    private readonly ITenantProvider _tenant;
    private readonly string[] _platformWidePrefixes;

    public TenantKeyedCacheService(ICacheService inner, ITenantProvider tenant, IEnumerable<string>? platformWidePrefixes = null)
    {
        _inner = inner;
        _tenant = tenant;
        _platformWidePrefixes = (platformWidePrefixes ?? ["settings:"]).ToArray();
    }

    /// <summary>The key to use in the shared store, or null when there is no tenant to key it by.</summary>
    private string? Scoped(string key)
    {
        foreach (var prefix in _platformWidePrefixes)
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                return key;

        return _tenant.CurrentTenantId is { } id && id != Guid.Empty ? $"t:{id:N}:{key}" : null;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        => Scoped(key) is { } k ? await _inner.GetAsync<T>(k, ct) : null;

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
        => Scoped(key) is { } k ? _inner.SetAsync(k, value, expiry, ct) : Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => Scoped(key) is { } k ? _inner.RemoveAsync(k, ct) : Task.CompletedTask;

    public Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
        => Scoped(pattern) is { } p ? _inner.RemoveByPatternAsync(p, ct) : Task.CompletedTask;

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Scoped(key) is { } k && await _inner.ExistsAsync(k, ct);

    public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
        => Scoped(key) is { } k ? _inner.GetOrSetAsync(k, factory, expiry, ct) : factory();
}

public static class TenantKeyedCacheExtensions
{
    /// <summary>
    /// Makes <see cref="ICacheService"/> tenant-keyed for this service. Call it <b>after</b> <c>AddCacheService</c> and after
    /// <c>AddRequestTenantProvider</c>. Keys starting with a platform-wide prefix (default <c>settings:</c>) are left as they are.
    /// </summary>
    public static IServiceCollection AddTenantKeyedCache(this IServiceCollection services, params string[] platformWidePrefixes)
    {
        var inner = services.LastOrDefault(d => d.ServiceType == typeof(ICacheService))
            ?? throw new ERP.Shared.Infrastructure.Exceptions.ServiceMisconfiguredException("AddTenantKeyedCache must be called after AddCacheService.");
        var innerType = inner.ImplementationType
            ?? throw new ERP.Shared.Infrastructure.Exceptions.ServiceMisconfiguredException("AddTenantKeyedCache expects the cache registered by AddCacheService (a concrete type).");

        services.Remove(inner);
        services.AddSingleton(innerType);
        var prefixes = platformWidePrefixes.Length > 0 ? platformWidePrefixes : ["settings:"];
        services.AddScoped<ICacheService>(sp => new TenantKeyedCacheService(
            (ICacheService)sp.GetRequiredService(innerType), sp.GetRequiredService<ITenantProvider>(), prefixes));
        return services;
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ERP.Shared.Authorization.ServiceAuth;

namespace ERP.Shared.Authorization;

public sealed record ResolvedTenant(Guid TenantId, string TenantCode, bool IsPlatform);

/// <summary>
/// Asks IdentityService (the owner of tenants and their domains) which tenant a request
/// host belongs to — GET {Tenancy:IdentityUrl}/internal/v1/tenants/resolve?host=… on the
/// internal network, with this service's own service token — and caches the answer briefly. Any failure resolves to "unknown", so the
/// caller fails closed instead of guessing a tenant. Used by the Gateway (every request)
/// and by hubs that bypass the Gateway (ChatBot guests).
/// </summary>
public sealed class TenantHostResolver
{
    public const string HttpClientName = "tenant-resolver";
    private static readonly TimeSpan PositiveTtl = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan NegativeTtl = TimeSpan.FromSeconds(10);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TenantHostResolver> _logger;

    public TenantHostResolver(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<TenantHostResolver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ResolvedTenant?> ResolveAsync(string host, CancellationToken ct)
    {
        var key = "tenant-host:" + host.Trim().ToLowerInvariant();
        if (_cache.TryGetValue(key, out ResolvedTenant? cached))
            return cached;

        ResolvedTenant? resolved = null;
        var cacheable = true;
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync($"internal/v1/tenants/resolve?host={Uri.EscapeDataString(host)}", ct);
            if (response.IsSuccessStatusCode)
                resolved = await response.Content.ReadFromJsonAsync<ResolvedTenant>(cancellationToken: ct);
            else if (response.StatusCode != HttpStatusCode.NotFound)
                cacheable = false; // transient Identity error — don't cache a false "unknown"
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Tenant resolution for host {Host} failed", host);
            cacheable = false;
        }

        if (cacheable)
            _cache.Set(key, resolved, resolved is null ? NegativeTtl : PositiveTtl);
        return resolved;
    }
}

public static class TenantHostResolverServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="TenantHostResolver"/> (singleton, in-memory cache) calling
    /// IdentityService at <c>Tenancy:IdentityUrl</c> (default http://identityservice:5001).
    /// </summary>
    public static IServiceCollection AddTenantHostResolver(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddSingleton<TenantHostResolver>();
        services.AddHttpClient(TenantHostResolver.HttpClientName, c =>
        {
            c.BaseAddress = new Uri((configuration["Tenancy:IdentityUrl"] ?? "http://identityservice:5001").TrimEnd('/') + "/");
            c.Timeout = TimeSpan.FromSeconds(3);
        })
            // Identity's /internal endpoint takes only service tokens; tenant-less, since this
            // call is how the caller learns the tenant.
            .AddServiceToken("identity", tenantless: true);
        return services;
    }
}

using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace Ocelot.API.Authorization;

public sealed record ResolvedTenant(Guid TenantId, string TenantCode, bool IsPlatform);

/// <summary>
/// Asks IdentityService (the owner of tenants and their domains) which tenant a request
/// host belongs to — GET {Tenancy:IdentityUrl}/api/v1/tenants/resolve?host=… on the internal
/// network — and caches the answer briefly. Any failure resolves to "unknown", so the
/// Gateway fails closed instead of guessing a tenant.
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
            using var response = await client.GetAsync($"api/v1/tenants/resolve?host={Uri.EscapeDataString(host)}", ct);
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

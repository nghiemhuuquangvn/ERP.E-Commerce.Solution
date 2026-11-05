using System.Net.Http.Json;
using ERP.NTF.Application.Interfaces;
using ERP.Shared.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.NTF.Infrastructure.Services;

/// <summary>
/// Asks IdentityService who holds a permission in the tenant of the message being handled (<c>GET internal/v1/permissions/{code}/users</c>, with this
/// service's own token). The answer is cached for a minute per tenant and permission. A failed call throws: the consumer retries the message instead of
/// telling nobody. A tenant is required (the consumers run inside <see cref="TenantScope"/>).
/// </summary>
public class ApproverDirectory : IApproverDirectory
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;

    public ApproverDirectory(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    private sealed record Holders(string Permission, List<Guid> UserIds);

    public async Task<IReadOnlyList<Guid>> GetUserIdsAsync(string permission, CancellationToken ct = default)
    {
        var tenant = TenantScope.Current;
        if (tenant == null || tenant == Guid.Empty)
            throw new HttpRequestException("No tenant: the approvers are looked up for one tenant.");

        var key = $"approvers:{tenant:N}:{permission}";
        if (_cache.TryGetValue(key, out IReadOnlyList<Guid>? cached) && cached != null) return cached;

        var answer = await _http.GetFromJsonAsync<Holders>($"internal/v1/permissions/{Uri.EscapeDataString(permission)}/users", ct)
                     ?? throw new HttpRequestException("IdentityService answered nothing.");
        IReadOnlyList<Guid> ids = answer.UserIds ?? new List<Guid>();
        _cache.Set(key, ids, CacheFor);
        return ids;
    }
}

namespace ERP.Identity.Application.Interfaces.Services;

/// <param name="IsPlatform">True for the platform (SYSTEM) tenant's host.</param>
public record TenantHostResolution(Guid TenantId, string TenantCode, bool IsPlatform);

public record TenantDomainDto(Guid Id, Guid TenantId, string Host, bool IsPrimary);

/// <summary>
/// Host → tenant mapping, the single source of "which tenant is this URL". Resolution order:
/// Tenancy:PlatformHost → SYSTEM; a registered TenantDomains host; "&lt;code&gt;.&lt;Tenancy:BaseDomain&gt;" (admin)
/// or "&lt;code&gt;.&lt;Tenancy:StorefrontBaseDomain&gt;" (storefront);
/// finally Tenancy:FallbackTenantCode (single-tenant / local development only).
/// Only active tenants resolve.
/// </summary>
public interface ITenantDomainService
{
    Task<TenantHostResolution?> ResolveAsync(string host, CancellationToken ct);
    Task<IReadOnlyList<TenantDomainDto>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<TenantDomainDto> AddAsync(Guid tenantId, string host, bool isPrimary, CancellationToken ct);
    Task RemoveAsync(Guid tenantId, Guid domainId, CancellationToken ct);
}

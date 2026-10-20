namespace ERP.Identity.Application.Interfaces.Services;

/// <summary>
/// The single rule for touching another tenant's data:
/// the caller's own tenant is always allowed; any OTHER tenant only if the caller is
/// signed in to the platform tenant AND holds the given platform permission (checked
/// against the database, not the token). Controllers use it declaratively through
/// [TenantRouteAccess] instead of hand-written checks.
/// </summary>
public interface ITenantAccessPolicy
{
    Task<bool> CanAccessTenantAsync(Guid targetTenantId, string platformPermission, CancellationToken ct);

    /// <summary>Caller is in the platform tenant and holds <paramref name="platformPermission"/>.</summary>
    Task<bool> HasPlatformPermissionAsync(string platformPermission, CancellationToken ct);
}

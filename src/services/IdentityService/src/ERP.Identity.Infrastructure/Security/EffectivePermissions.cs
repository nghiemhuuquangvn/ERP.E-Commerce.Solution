using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Shared.Authorization;

namespace ERP.Identity.Infrastructure.Security;

/// <summary>
/// The one place that decides which permission codes go into a token (REST login,
/// 2FA, refresh, external login, OIDC). Every issuance path must call this — never
/// put raw role permissions into a token.
/// </summary>
public static class EffectivePermissions
{
    public static async Task<IReadOnlyList<string>> ResolveAsync(
        IUnitOfWork unitOfWork, Guid userId, Guid tenantId, IEnumerable<string> roleCodes, CancellationToken ct)
    {
        IEnumerable<string> codes;

        if (PlatformTenant.Is(tenantId)
            && roleCodes.Any(code => code.Equals(PlatformRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
        {
            // The platform super-admin holds the entire catalog — derived here (DB role in the
            // platform tenant, not a forgeable claim) so a newly added permission never needs
            // a per-migration grant, now that the role-name bypass in AuthorizePermission is gone.
            codes = (await unitOfWork.Permissions.ListAllAsync(ct)).Select(p => p.PermissionCode);
        }
        else
        {
            codes = await unitOfWork.Permissions.GetPermissionAsync(userId, tenantId, ct);
        }

        // Platform permissions are only effective in the platform tenant — even if bad data
        // links one to a role elsewhere, it never reaches a token for another tenant.
        return PermissionScopes.EffectiveIn(codes.Distinct(StringComparer.OrdinalIgnoreCase), tenantId);
    }

    /// <summary>Same as <see cref="ResolveAsync"/>, loading the user's roles in the tenant first.</summary>
    public static async Task<IReadOnlyList<string>> ResolveForUserAsync(
        IUnitOfWork unitOfWork, Guid userId, Guid tenantId, CancellationToken ct)
    {
        var roles = await unitOfWork.Roles.GetRolesAsync(userId, tenantId, ct);
        return await ResolveAsync(unitOfWork, userId, tenantId, roles, ct);
    }

    /// <summary>
    /// Anti-escalation rule shared by role grants and API keys: a permission can be handed
    /// out in <paramref name="tenantId"/> only if it is effective there (platform permissions
    /// only in the platform tenant) and the granting user holds it themselves.
    /// Returns the reason it can't be granted, or null when it can.
    /// </summary>
    public static async Task<string?> WhyNotGrantableAsync(
        IUnitOfWork unitOfWork, string permissionCode, Guid tenantId, Guid? grantedByUserId, CancellationToken ct)
    {
        if (!PermissionScopes.IsGrantableIn(permissionCode, tenantId))
            return $"Permission '{permissionCode}' is platform-scoped and can only be granted in the platform tenant.";

        if (grantedByUserId is null)
            return "The granting user is unknown.";

        var granterPermissions = await ResolveForUserAsync(unitOfWork, grantedByUserId.Value, tenantId, ct);
        return granterPermissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase)
            ? null
            : $"You can only grant permissions you hold yourself ('{permissionCode}').";
    }
}

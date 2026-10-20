namespace ERP.Shared.Authorization;

/// <summary>
/// The platform tenant: the seeded SYSTEM tenant that operates the whole platform.
/// Only tokens issued for this tenant (and therefore only usable on its own host —
/// see the Gateway's tenant-from-host binding) may carry platform permissions.
/// </summary>
public static class PlatformTenant
{
    public static readonly Guid Id = new("00000000-0000-0000-0000-000000000001");

    public const string Code = "SYSTEM";

    public static bool Is(Guid tenantId) => tenantId == Id;
}

/// <summary>
/// Role codes with platform meaning. They may only exist in the platform tenant, so a
/// tenant administrator can never mint a role that looks like a platform role.
/// </summary>
public static class PlatformRoles
{
    /// <summary>Code of the seeded platform super-admin role (001_CreateAuthDB.sql, SYSTEM tenant).</summary>
    public const string SuperAdmin = "SuperAdmin";

    // Compared after removing separators, case-insensitively — so "Super_Admin",
    // "super-admin" and "SUPER ADMIN" are all caught. Keep in sync with
    // Database/Identity/021_EnforcePlatformPermissionScope.sql.
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "SUPERADMIN", "SYSTEM", "PLATFORMADMIN", "ROOT",
    };

    public static bool IsReserved(string? roleCode) =>
        !string.IsNullOrWhiteSpace(roleCode)
        && Reserved.Contains(new string(roleCode.Where(char.IsLetterOrDigit).ToArray()));
}

/// <summary>
/// Single source of truth for "which permissions cross tenant boundaries".
///
/// Tenant-scope permissions act only on the caller's own tenant. Platform-scope
/// permissions manage the platform itself or other tenants; they are effective only in
/// the <see cref="PlatformTenant"/>:
/// - IdentityService strips them from every token issued for any other tenant;
/// - they cannot be granted to a role, API key or self-signup admin outside it;
/// - cross-tenant endpoints require the caller to be in the platform tenant AND hold one.
/// Adding a new cross-tenant capability = add its permission code here; nothing else.
/// </summary>
public static class PermissionScopes
{
    private static readonly HashSet<string> PlatformPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        PermissionConstants.Identity_TenantsView,
        PermissionConstants.Identity_TenantsCreate,
        PermissionConstants.Identity_TenantsEdit,
        PermissionConstants.Identity_TenantsManage,
        PermissionConstants.Identity_TenantsDelete,
        PermissionConstants.Identity_PermissionsManage,
        PermissionConstants.Settings_SystemView,
        PermissionConstants.Settings_SystemManage,
        PermissionConstants.License_ModulesManage,
        PermissionConstants.License_PlansView,
        PermissionConstants.License_PlansManage,
        PermissionConstants.License_TenantIssue,
    };

    public static IReadOnlyCollection<string> Platform => PlatformPermissions;

    public static bool IsPlatform(string permissionCode) => PlatformPermissions.Contains(permissionCode);

    /// <summary>Whether <paramref name="permissionCode"/> may be held by anyone in <paramref name="tenantId"/>.</summary>
    public static bool IsGrantableIn(string permissionCode, Guid tenantId) =>
        !IsPlatform(permissionCode) || PlatformTenant.Is(tenantId);

    /// <summary>Drops every permission not effective in <paramref name="tenantId"/>.</summary>
    public static IReadOnlyList<string> EffectiveIn(IEnumerable<string> permissionCodes, Guid tenantId) =>
        permissionCodes.Where(code => IsGrantableIn(code, tenantId)).ToList();
}

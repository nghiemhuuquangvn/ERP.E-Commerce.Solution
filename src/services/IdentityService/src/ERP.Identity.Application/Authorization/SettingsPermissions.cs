namespace ERP.Identity.Application.Authorization;

/// <summary>
/// Permission codes for auth-settings management. Unlike <see cref="Permissions"/> (legacy
/// snake_case), these follow the colon convention actually seeded by
/// src/Database/Identity/001_CreateAuthDB.sql (e.g. "users:manage", "system:admin").
/// </summary>
public static class SettingsPermissions
{
    public const string SystemView = "settings:system:view";
    public const string SystemManage = "settings:system:manage";
    public const string TenantView = "settings:tenant:view";
    public const string TenantManage = "settings:tenant:manage";
}

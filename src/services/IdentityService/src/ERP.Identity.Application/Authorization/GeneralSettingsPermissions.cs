namespace ERP.Identity.Application.Authorization;

/// <summary>Permission codes for General Settings management. Same colon convention as SettingsPermissions.</summary>
public static class GeneralSettingsPermissions
{
    public const string SystemView = "settings:general:system:view";
    public const string SystemManage = "settings:general:system:manage";
    public const string TenantView = "settings:general:tenant:view";
    public const string TenantManage = "settings:general:tenant:manage";
}

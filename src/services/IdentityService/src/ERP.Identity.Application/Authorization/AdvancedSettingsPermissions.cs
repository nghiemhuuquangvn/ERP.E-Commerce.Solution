namespace ERP.Identity.Application.Authorization;

/// <summary>Permission codes for Advanced Settings management.</summary>
public static class AdvancedSettingsPermissions
{
    public const string SystemView = "settings:advanced:system:view";
    public const string SystemManage = "settings:advanced:system:manage";
    public const string TenantView = "settings:advanced:tenant:view";
    public const string TenantManage = "settings:advanced:tenant:manage";
}

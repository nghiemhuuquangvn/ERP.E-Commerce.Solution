namespace ERP.Identity.Application.Authorization;

/// <summary>Permission codes for Localization Settings management.</summary>
public static class LocalizationSettingsPermissions
{
    public const string SystemView = "settings:localization:system:view";
    public const string SystemManage = "settings:localization:system:manage";
    public const string TenantView = "settings:localization:tenant:view";
    public const string TenantManage = "settings:localization:tenant:manage";
}

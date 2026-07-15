namespace ERP.Identity.Application.Authorization;

/// <summary>Permission codes for Integration Settings management.</summary>
public static class IntegrationSettingsPermissions
{
    public const string SystemView = "settings:integration:system:view";
    public const string SystemManage = "settings:integration:system:manage";
    public const string TenantView = "settings:integration:tenant:view";
    public const string TenantManage = "settings:integration:tenant:manage";
}

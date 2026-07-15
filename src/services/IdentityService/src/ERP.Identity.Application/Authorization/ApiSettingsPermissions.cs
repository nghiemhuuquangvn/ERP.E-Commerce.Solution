namespace ERP.Identity.Application.Authorization;

/// <summary>Permission codes for API Settings management. Distinct from ApiKeysController's own permissions.</summary>
public static class ApiSettingsPermissions
{
    public const string SystemView = "settings:api:system:view";
    public const string SystemManage = "settings:api:system:manage";
    public const string TenantView = "settings:api:tenant:view";
    public const string TenantManage = "settings:api:tenant:manage";
}

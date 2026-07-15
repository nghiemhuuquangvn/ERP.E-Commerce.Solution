namespace ERP.Identity.Application.Authorization;

/// <summary>Permission codes for Audit Settings management.</summary>
public static class AuditSettingsPermissions
{
    public const string SystemView = "settings:audit:system:view";
    public const string SystemManage = "settings:audit:system:manage";
    public const string TenantView = "settings:audit:tenant:view";
    public const string TenantManage = "settings:audit:tenant:manage";
}

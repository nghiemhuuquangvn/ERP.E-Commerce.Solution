namespace ERP.Identity.Application.Authorization;

/// <summary>Permission codes for Workflow Settings management. Same colon convention as SettingsPermissions.</summary>
public static class WorkflowSettingsPermissions
{
    public const string SystemView = "settings:workflow:system:view";
    public const string SystemManage = "settings:workflow:system:manage";
    public const string TenantView = "settings:workflow:tenant:view";
    public const string TenantManage = "settings:workflow:tenant:manage";
}

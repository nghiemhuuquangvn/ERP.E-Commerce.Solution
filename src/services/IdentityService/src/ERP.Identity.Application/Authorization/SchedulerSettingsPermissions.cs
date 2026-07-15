namespace ERP.Identity.Application.Authorization;

/// <summary>Permission codes for Scheduler Settings management.</summary>
public static class SchedulerSettingsPermissions
{
    public const string SystemView = "settings:scheduler:system:view";
    public const string SystemManage = "settings:scheduler:system:manage";
    public const string TenantView = "settings:scheduler:tenant:view";
    public const string TenantManage = "settings:scheduler:tenant:manage";
}

namespace ERP.Identity.Application.Authorization;

/// <summary>Permission codes for File Storage Settings management.</summary>
public static class FileStorageSettingsPermissions
{
    public const string SystemView = "settings:file-storage:system:view";
    public const string SystemManage = "settings:file-storage:system:manage";
    public const string TenantView = "settings:file-storage:tenant:view";
    public const string TenantManage = "settings:file-storage:tenant:manage";
}

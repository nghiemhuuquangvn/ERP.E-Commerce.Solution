namespace ERP.Identity.Application.Authorization;

/// <summary>Permission codes for Feature Flags management.</summary>
public static class FeatureFlagsPermissions
{
    public const string SystemView = "settings:feature-flags:system:view";
    public const string SystemManage = "settings:feature-flags:system:manage";
    public const string TenantView = "settings:feature-flags:tenant:view";
    public const string TenantManage = "settings:feature-flags:tenant:manage";
}

namespace ERP.Identity.Application.Authorization;

/// <summary>Permission codes for Company Settings management. No System tier — tenant-only resource.</summary>
public static class CompanySettingsPermissions
{
    public const string TenantView = "settings:company:tenant:view";
    public const string TenantManage = "settings:company:tenant:manage";
}

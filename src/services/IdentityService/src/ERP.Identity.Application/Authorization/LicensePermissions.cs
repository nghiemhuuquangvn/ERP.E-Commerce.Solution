namespace ERP.Identity.Application.Authorization;

/// <summary>Permission codes for License management (module catalog, plan catalog, tenant licenses).</summary>
public static class LicensePermissions
{
    public const string ModulesManage = "license:modules:manage";
    public const string PlansView = "license:plans:view";
    public const string PlansManage = "license:plans:manage";
    public const string TenantView = "license:tenant:view";
    public const string TenantIssue = "license:tenant:issue";
}

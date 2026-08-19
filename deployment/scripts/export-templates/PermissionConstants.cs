namespace ERP.Shared.Authorization;

/// <summary>
/// Permission code catalog for a standalone IdentityService deployment.
///
/// This is a trimmed replacement for the monorepo's PermissionConstants.cs, generated
/// by deployment/scripts/export-identity-service.ps1 — the original file also defines
/// ~230 business-domain permission codes (crm:*, sales_order:*, payments:*, ...) for
/// the other 13 services in that monorepo, none of which mean anything to a
/// standalone identity service. This file keeps exactly the permission codes
/// IdentityService's own controllers reference by name (identity:*/security:*/
/// license:*/settings:*), plus a Sample_* section demonstrating the pattern for your
/// own application's permissions — replace or extend that section for your domain.
/// </summary>
public static class PermissionConstants
{
    // Identity: user/role/permission/tenant management
    public const string Identity_UsersView = "identity:users:view";
    public const string Identity_UsersCreate = "identity:users:create";
    public const string Identity_UsersEdit = "identity:users:edit";
    public const string Identity_UsersManage = "identity:users:manage";
    public const string Identity_UsersDelete = "identity:users:delete";
    public const string Identity_RolesView = "identity:roles:view";
    public const string Identity_RolesCreate = "identity:roles:create";
    public const string Identity_RolesEdit = "identity:roles:edit";
    public const string Identity_RolesManage = "identity:roles:manage";
    public const string Identity_RolesDelete = "identity:roles:delete";
    public const string Identity_PermissionsView = "identity:permissions:view";
    public const string Identity_PermissionsManage = "identity:permissions:manage";
    public const string Identity_TenantsView = "identity:tenants:view";
    public const string Identity_TenantsCreate = "identity:tenants:create";
    public const string Identity_TenantsEdit = "identity:tenants:edit";
    public const string Identity_TenantsManage = "identity:tenants:manage";
    public const string Identity_TenantsDelete = "identity:tenants:delete";

    // Security: audit log, API keys, webhooks
    public const string Security_AuditView = "security:audit:view";
    public const string Security_ApiKeysView = "security:apikeys:view";
    public const string Security_ApiKeysManage = "security:apikeys:manage";
    public const string Security_WebhooksView = "security:webhooks:view";
    public const string Security_WebhooksManage = "security:webhooks:manage";

    // License: module catalog, plan catalog, tenant licenses
    public const string License_ModulesManage = "license:modules:manage";
    public const string License_PlansView = "license:plans:view";
    public const string License_PlansManage = "license:plans:manage";
    public const string License_TenantView = "license:tenant:view";
    public const string License_TenantIssue = "license:tenant:issue";

    // Settings: platform/system-tier categories, {system|tenant} x {view|manage}
    public const string Settings_SystemView = "settings:system:view";
    public const string Settings_SystemManage = "settings:system:manage";
    public const string Settings_TenantView = "settings:tenant:view";
    public const string Settings_TenantManage = "settings:tenant:manage";

    public const string Settings_General_SystemView = "settings:general:system:view";
    public const string Settings_General_SystemManage = "settings:general:system:manage";
    public const string Settings_General_TenantView = "settings:general:tenant:view";
    public const string Settings_General_TenantManage = "settings:general:tenant:manage";

    // Company Settings has no System tier — tenant-only resource
    public const string Settings_Company_TenantView = "settings:company:tenant:view";
    public const string Settings_Company_TenantManage = "settings:company:tenant:manage";

    public const string Settings_Scheduler_SystemView = "settings:scheduler:system:view";
    public const string Settings_Scheduler_SystemManage = "settings:scheduler:system:manage";
    public const string Settings_Scheduler_TenantView = "settings:scheduler:tenant:view";
    public const string Settings_Scheduler_TenantManage = "settings:scheduler:tenant:manage";

    public const string Settings_Integration_SystemView = "settings:integration:system:view";
    public const string Settings_Integration_SystemManage = "settings:integration:system:manage";
    public const string Settings_Integration_TenantView = "settings:integration:tenant:view";
    public const string Settings_Integration_TenantManage = "settings:integration:tenant:manage";

    public const string Settings_Api_SystemView = "settings:api:system:view";
    public const string Settings_Api_SystemManage = "settings:api:system:manage";
    public const string Settings_Api_TenantView = "settings:api:tenant:view";
    public const string Settings_Api_TenantManage = "settings:api:tenant:manage";

    public const string Settings_Localization_SystemView = "settings:localization:system:view";
    public const string Settings_Localization_SystemManage = "settings:localization:system:manage";
    public const string Settings_Localization_TenantView = "settings:localization:tenant:view";
    public const string Settings_Localization_TenantManage = "settings:localization:tenant:manage";

    public const string Settings_Audit_SystemView = "settings:audit:system:view";
    public const string Settings_Audit_SystemManage = "settings:audit:system:manage";
    public const string Settings_Audit_TenantView = "settings:audit:tenant:view";
    public const string Settings_Audit_TenantManage = "settings:audit:tenant:manage";

    public const string Settings_FileStorage_SystemView = "settings:file-storage:system:view";
    public const string Settings_FileStorage_SystemManage = "settings:file-storage:system:manage";
    public const string Settings_FileStorage_TenantView = "settings:file-storage:tenant:view";
    public const string Settings_FileStorage_TenantManage = "settings:file-storage:tenant:manage";

    public const string Settings_Advanced_SystemView = "settings:advanced:system:view";
    public const string Settings_Advanced_SystemManage = "settings:advanced:system:manage";
    public const string Settings_Advanced_TenantView = "settings:advanced:tenant:view";
    public const string Settings_Advanced_TenantManage = "settings:advanced:tenant:manage";

    public const string Settings_Workflow_SystemView = "settings:workflow:system:view";
    public const string Settings_Workflow_SystemManage = "settings:workflow:system:manage";
    public const string Settings_Workflow_TenantView = "settings:workflow:tenant:view";
    public const string Settings_Workflow_TenantManage = "settings:workflow:tenant:manage";

    public const string Settings_FeatureFlags_SystemView = "settings:feature-flags:system:view";
    public const string Settings_FeatureFlags_SystemManage = "settings:feature-flags:system:manage";
    public const string Settings_FeatureFlags_TenantView = "settings:feature-flags:tenant:view";
    public const string Settings_FeatureFlags_TenantManage = "settings:feature-flags:tenant:manage";

    // Sample_* — illustrative only. Replace with your own application's permission
    // vocabulary; nothing in IdentityService's own code references these by name.
    public const string Sample_WidgetsView = "sample:widgets:view";
    public const string Sample_WidgetsManage = "sample:widgets:manage";
}

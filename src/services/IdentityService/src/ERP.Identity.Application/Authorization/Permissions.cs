namespace ERP.Identity.Application.Authorization;
public static class Permissions
{
    // Users
    public const string ManageUsers = "manage_users";
    public const string ViewUsers = "view_users";
    public const string CreateUsers = "create_users";
    public const string EditUsers = "edit_users";
    public const string DeleteUsers = "delete_users";

    // Roles
    public const string ManageRoles = "manage_roles";
    public const string ViewRoles = "view_roles";
    public const string CreateRoles = "create_roles";
    public const string EditRoles = "edit_roles";
    public const string DeleteRoles = "delete_roles";

    // Permissions
    public const string ManagePermissions = "manage_permissions";
    public const string ViewPermissions = "view_permissions";

    // Tenants
    public const string ManageTenants = "manage_tenants";
    public const string ViewTenants = "view_tenants";
    public const string CreateTenants = "create_tenants";
    public const string EditTenants = "edit_tenants";
    public const string DeleteTenants = "delete_tenants";

    // Security (audit logs, login attempts)
    public const string ViewAuditLogs = "security:audit:view";

    // API keys (service-to-service credentials)
    public const string ViewApiKeys = "security:apikeys:view";
    public const string ManageApiKeys = "security:apikeys:manage";
}
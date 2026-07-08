namespace ERP.Identity.Application.Authorization;
public static class Permissions
{
    // Users
    public const string ManageUsers = "identity:users:manage";
    public const string ViewUsers = "identity:users:view";
    public const string CreateUsers = "identity:users:create";
    public const string EditUsers = "identity:users:edit";
    public const string DeleteUsers = "identity:users:delete";

    // Roles
    public const string ManageRoles = "identity:roles:manage";
    public const string ViewRoles = "identity:roles:view";
    public const string CreateRoles = "identity:roles:create";
    public const string EditRoles = "identity:roles:edit";
    public const string DeleteRoles = "identity:roles:delete";

    // Permissions
    public const string ManagePermissions = "identity:permissions:manage";
    public const string ViewPermissions = "identity:permissions:view";

    // Tenants
    public const string ManageTenants = "identity:tenants:manage";
    public const string ViewTenants = "identity:tenants:view";
    public const string CreateTenants = "identity:tenants:create";
    public const string EditTenants = "identity:tenants:edit";
    public const string DeleteTenants = "identity:tenants:delete";

    // Security (audit logs, login attempts)
    public const string ViewAuditLogs = "security:audit:view";

    // API keys (service-to-service credentials)
    public const string ViewApiKeys = "security:apikeys:view";
    public const string ManageApiKeys = "security:apikeys:manage";
}

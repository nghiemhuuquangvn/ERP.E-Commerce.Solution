namespace ERP.Identity.Infrastructure.Mapping;
using ERP.Identity.Application.Interfaces.Mapping;
using ERP.Identity.Application.Authorization;

public class RolePermissionResolver : IRolePermissionResolver
{
    private readonly Dictionary<string, IReadOnlyCollection<string>> _rolePermissions = new()
    {
        { "Admin", new[] { Permissions.ManageUsers, Permissions.ViewUsers, Permissions.CreateUsers, Permissions.EditUsers, Permissions.DeleteUsers } },
        { "User", new[] { Permissions.ViewUsers } }
    };

    public IReadOnlyCollection<string> GetPermissionsForRole(string roleCode)
    {
        return _rolePermissions.TryGetValue(roleCode, out var permissions) ? permissions : Array.Empty<string>();
    }
}
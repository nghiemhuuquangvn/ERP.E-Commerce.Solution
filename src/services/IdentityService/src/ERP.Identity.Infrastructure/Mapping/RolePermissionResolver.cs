namespace ERP.Identity.Infrastructure.Mapping;
using ERP.Identity.Application.Interfaces.Mapping;
using ERP.Shared.Authorization;

public class RolePermissionResolver : IRolePermissionResolver
{
    private readonly Dictionary<string, IReadOnlyCollection<string>> _rolePermissions = new()
    {
        { "Admin", new[] { PermissionConstants.Identity_UsersManage, PermissionConstants.Identity_UsersView, PermissionConstants.Identity_UsersCreate, PermissionConstants.Identity_UsersEdit, PermissionConstants.Identity_UsersDelete } },
        { "User", new[] { PermissionConstants.Identity_UsersView } }
    };

    public IReadOnlyCollection<string> GetPermissionsForRole(string roleCode)
    {
        return _rolePermissions.TryGetValue(roleCode, out var permissions) ? permissions : Array.Empty<string>();
    }
}
namespace ERP.Identity.Application.Interfaces.Mapping;
public interface IRolePermissionResolver
{
    IReadOnlyCollection<string> GetPermissionsForRole(string roleCode);
}
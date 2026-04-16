namespace ERP.Identity.Application.Interfaces.Mapping;
public interface IUserRoleResolver
{
    IReadOnlyList<string> GetRolesForUser(UserType userType);
}
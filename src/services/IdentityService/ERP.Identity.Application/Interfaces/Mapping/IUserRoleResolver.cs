namespace ERP.Identity.Application.Interfaces.Mapping;
public interface IUserRoleResolver
{
    IReadOnlyCollection<string> GetRolesForUser(UserType userType);
}
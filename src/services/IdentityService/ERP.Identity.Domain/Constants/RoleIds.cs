
namespace ERP.Identity.Domain.Constants;
public static class RoleIds
{
    public static readonly Guid Admin = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static Guid FromCode(string roleCode)
    {
        return roleCode.ToLower() switch
        {
            SystemRoles.Admin => Admin,
            SystemRoles.User => User,
            _ => throw new ArgumentException("Invalid role code", nameof(roleCode)),
        };
    }
}
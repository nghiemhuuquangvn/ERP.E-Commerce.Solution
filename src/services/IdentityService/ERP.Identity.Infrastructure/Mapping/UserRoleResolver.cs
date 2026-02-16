using ERP.Identity.Domain.Constants;
using ERP.Identity.Application.Interfaces.Mapping;

namespace ERP.Identity.Infrastructure.Mapping;
public sealed class UserRoleResolver : IUserRoleResolver
{
    public IReadOnlyCollection<string> GetRolesForUser(UserType userType)
    {
        return userType switch
        {
            UserType.Customer => new[] { SystemRoles.Customer },
            UserType.Staff => new[] { SystemRoles.Staff },
            UserType.Admin => new[] { SystemRoles.Admin },
            UserType.SuperAdmin => new[] { SystemRoles.SuperAdmin },
            UserType.System => new[] { SystemRoles.System },
            _ => throw new ArgumentOutOfRangeException(nameof(userType), $"Unsupported user type: {userType}")
        };
    }
}
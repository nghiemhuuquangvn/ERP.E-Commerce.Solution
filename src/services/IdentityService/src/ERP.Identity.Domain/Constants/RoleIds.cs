
namespace ERP.Identity.Domain.Constants;
public static class RoleIds
{
    public static readonly Guid SuperAdmin = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid Admin      = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid Staff      = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Customer   = Guid.Parse("00000000-0000-0000-0000-000000000000");

    public static Guid FromCode(string roleCode)
    {
        return roleCode.ToUpperInvariant() switch
        {
            SystemRoles.SuperAdmin => SuperAdmin,
            SystemRoles.Admin      => Admin,
            SystemRoles.Staff      => Staff,
            SystemRoles.Customer   => Customer,
            SystemRoles.User       => Customer, // Map User về Customer nếu cần
            _ => throw new ArgumentException($"Role code '{roleCode}' is not supported.", nameof(roleCode)),
        };
    }
}
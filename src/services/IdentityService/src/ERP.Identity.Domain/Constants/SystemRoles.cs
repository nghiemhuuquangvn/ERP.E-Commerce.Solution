namespace ERP.Identity.Domain.Constants;
public static class SystemRoles
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string Admin = "ADMIN";
    public const string User = "USER";
    public const string Customer = "CUSTOMER";
    public const string Staff = "STAFF";
    public const string System = "SYSTEM";

    /// <summary>Roles every tenant gets when it is created (<c>DefaultTenantRoles</c>); self-service sign-up assigns them, so they cannot be deleted.</summary>
    public static bool IsDefaultTenantRole(string? code) =>
        string.Equals(code, Customer, StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, Staff, StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, User, StringComparison.OrdinalIgnoreCase);
}
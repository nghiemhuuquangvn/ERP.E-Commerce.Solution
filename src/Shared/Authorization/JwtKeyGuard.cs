namespace ERP.Shared.Authorization;

/// <summary>
/// Refuses well-known, non-secret JWT signing keys outside Development. The symmetric
/// Jwt:Key is shared by IdentityService (signs) and every service + the Gateway
/// (validates), so a publicly known value lets anyone mint a token for any user/tenant/role.
/// The values below are known because they are (or were) committed to this repository —
/// the old shared dev key leaked via appsettings.*.json in git history, and the two
/// placeholders ship in appsettings.json / .env.example. All pass the 32-byte length check,
/// so length alone is not enough.
/// </summary>
public static class JwtKeyGuard
{
    private static readonly HashSet<string> KnownPublicKeys = new(StringComparer.Ordinal)
    {
        "97-prKep1shfkBdXXqU8fngCUE3n8LG2yqhkgJXTJGjLId8lf9bdGs8fVWDlwZ-a",
        "CHANGE_ME_SET_VIA_JWT__KEY_ENVIRONMENT_VARIABLE",
        "ChangeMe_Minimum_256_Bits_Required!",
    };

    public static void EnsureUsable(string jwtKey, bool isDevelopment)
    {
        if (!isDevelopment && KnownPublicKeys.Contains(jwtKey))
            throw new InvalidOperationException(
                "Configuration value 'Jwt:Key' is a publicly known placeholder/leaked key and must not be used outside Development. " +
                "Set a fresh random secret via the Jwt__Key environment variable (e.g. JWT_SIGNING_KEY in .env).");
    }

    /// <summary>For call sites without an IHostEnvironment (e.g. AddErpJwtAuthentication).</summary>
    public static bool IsDevelopmentFromEnvironmentVariables()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        return string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
    }
}

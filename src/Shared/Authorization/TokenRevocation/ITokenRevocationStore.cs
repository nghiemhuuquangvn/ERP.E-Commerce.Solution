namespace ERP.Shared.Authorization.TokenRevocation;

/// <summary>
/// What the revocation store knows about one access token: whether its <c>jti</c> was revoked
/// (logout) and the unix-seconds time of the last security-relevant change of its user and of
/// its tenant. A token issued (<c>iat</c>) before either change is stale.
/// </summary>
public sealed record RevocationState(bool JtiRevoked, long? UserChangedAt, long? TenantChangedAt)
{
    public static readonly RevocationState None = new(false, null, null);
}

/// <summary>
/// Platform-wide store that makes logout, account lock/deactivation, password changes and
/// role/permission/license changes take effect in EVERY service immediately instead of when
/// the access token expires. IdentityService writes; every service reads (through
/// <see cref="TokenRevocationValidator"/>, wired into AddErpJwtAuthentication).
/// Keys live under one fixed prefix (<see cref="TokenRevocationKeys"/>), independent of each
/// service's own cache prefix, so all services see the same entries.
/// </summary>
public interface ITokenRevocationStore
{
    Task<RevocationState> GetStateAsync(string? jti, string? userId, string? tenantId, CancellationToken ct = default);

    /// <summary>Revokes one access token until it would have expired anyway (logout).</summary>
    Task RevokeTokenAsync(string jti, DateTime expiresAtUtc, CancellationToken ct = default);

    /// <summary>Invalidates every token the users hold now (they must refresh or sign in again).</summary>
    Task MarkUsersChangedAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);

    /// <summary>Invalidates every token of a tenant (license or tenant status changed).</summary>
    Task MarkTenantChangedAsync(Guid tenantId, CancellationToken ct = default);
}

public static class TokenRevocationKeys
{
    public const string Prefix = "erp:authz:";

    public static string RevokedJti(string jti) => $"{Prefix}revoked-jti:{jti}";
    public static string UserChangedAt(string userId) => $"{Prefix}ver:user:{userId.ToLowerInvariant()}";
    public static string TenantChangedAt(string tenantId) => $"{Prefix}ver:tenant:{tenantId.ToLowerInvariant()}";
}

public sealed class TokenRevocationOptions
{
    /// <summary>How long a validated state is reused locally before Redis is asked again
    /// (the worst-case delay before a revocation is seen). Default 5 s.</summary>
    public int LocalCacheSeconds { get; set; } = 5;

    /// <summary>How long a "user/tenant changed at" marker is kept — must exceed the longest
    /// access-token lifetime. Default 24 h.</summary>
    public int ChangeMarkerTtlMinutes { get; set; } = 24 * 60;
}

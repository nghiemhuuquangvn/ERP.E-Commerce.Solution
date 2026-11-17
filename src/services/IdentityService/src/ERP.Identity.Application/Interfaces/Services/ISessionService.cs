using ERP.Identity.Application.DTOs.Security;

namespace ERP.Identity.Application.Interfaces.Services
{
    /// <summary>
    /// Covers both DeviceSessions ("where am I logged in") and TokenBlacklist
    /// (immediate single-token revocation) — the two tables the business-analysis
    /// doc grouped together under "Session management". Does NOT call
    /// SaveChangesAsync — callers control the transaction boundary.
    /// </summary>
    public interface ISessionService
    {
        /// <summary>Creates or reactivates the user's row for this device; returns its id (the refresh tokens carry it).</summary>
        Task<Guid> RegisterSessionAsync(
            Guid userId, Guid tenantId, string? deviceId, string? deviceName,
            string? deviceType, string? platform, string? ipAddress, string? userAgent, CancellationToken ct);

        /// <summary>
        /// Marks the device session as used now (a refresh). False when it no longer exists or was revoked - the
        /// refresh must then be refused (SEC-M4).
        /// </summary>
        Task<bool> TouchSessionAsync(Guid deviceSessionId, string? ipAddress, CancellationToken ct);

        Task<IReadOnlyList<DeviceSessionDto>> ListActiveSessionsAsync(Guid userId, CancellationToken ct);

        /// <summary>Ends one device session and every refresh token issued for it (SEC-M4). The one method here that
        /// commits (SaveChangesAsync) itself: it is a stand-alone user action.</summary>
        Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct);

        /// <summary>
        /// Revokes every refresh token and active device session of a user — used when the
        /// account's credentials or status change (deactivate, password reset/change) so an
        /// existing session can't keep minting new access tokens. Does NOT call
        /// SaveChangesAsync; callers control the transaction boundary.
        /// </summary>
        Task RevokeAllForUserAsync(Guid userId, CancellationToken ct);

        Task BlacklistTokenAsync(string jti, Guid userId, DateTime expiresAt, string? revokedBy, CancellationToken ct);

        Task<bool> IsTokenBlacklistedAsync(string jti, CancellationToken ct);
    }
}

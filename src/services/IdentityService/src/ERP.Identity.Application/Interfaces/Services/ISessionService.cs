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
        Task RegisterSessionAsync(
            Guid userId, Guid tenantId, string? deviceId, string? deviceName,
            string? deviceType, string? platform, string? ipAddress, string? userAgent, CancellationToken ct);

        Task<IReadOnlyList<DeviceSessionDto>> ListActiveSessionsAsync(Guid userId, CancellationToken ct);

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

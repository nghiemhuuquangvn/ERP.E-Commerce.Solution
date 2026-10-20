using ERP.Identity.Application.DTOs.Security;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Identity.Infrastructure.Services
{
    public class SessionService : ISessionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SessionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task RegisterSessionAsync(
            Guid userId, Guid tenantId, string? deviceId, string? deviceName,
            string? deviceType, string? platform, string? ipAddress, string? userAgent, CancellationToken ct)
        {
            // Clients that don't send an explicit X-Device-Id (most don't yet) still get
            // tracked, grouped by a stable hash of their User-Agent — an honest fallback,
            // not a precise per-install one, but better than not tracking sessions at all.
            var effectiveDeviceId = string.IsNullOrWhiteSpace(deviceId)
                ? HashUserAgent(userAgent)
                : deviceId;

            var existing = await _unitOfWork.DeviceSessions.GetByUserAndDeviceAsync(userId, effectiveDeviceId, ct);
            if (existing != null)
            {
                existing.Touch(ipAddress);
                await _unitOfWork.DeviceSessions.UpdateAsync(existing, ct);
                return;
            }

            var session = DeviceSession.Create(userId, tenantId, effectiveDeviceId, deviceName, deviceType, platform, ipAddress);
            await _unitOfWork.DeviceSessions.AddAsync(session, ct);
        }

        public async Task<IReadOnlyList<DeviceSessionDto>> ListActiveSessionsAsync(Guid userId, CancellationToken ct)
        {
            var sessions = await _unitOfWork.DeviceSessions.ListActiveByUserAsync(userId, ct);
            return sessions.Select(s => new DeviceSessionDto(
                s.Id, s.DeviceId, s.DeviceName, s.DeviceType, s.Platform, s.IpAddress, s.IsActive, s.LastActiveAt, s.CreatedAt)).ToList();
        }

        public async Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct)
        {
            var session = await _unitOfWork.DeviceSessions.GetByIdAsync(sessionId, ct);
            if (session == null || session.UserId != userId)
                throw new InvalidOperationException("Session not found");

            session.Revoke();
            await _unitOfWork.DeviceSessions.UpdateAsync(session, ct);
        }

        public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct)
        {
            await _unitOfWork.RefreshTokens.RevokeAllByUserIdAsync(userId, ct);

            // ListActiveByUserAsync reads AsNoTracking — the revoke must be attached via
            // UpdateAsync or it is silently never persisted.
            var activeSessions = await _unitOfWork.DeviceSessions.ListActiveByUserAsync(userId, ct);
            foreach (var activeSession in activeSessions)
            {
                activeSession.Revoke();
                await _unitOfWork.DeviceSessions.UpdateAsync(activeSession, ct);
            }
        }

        public async Task BlacklistTokenAsync(string jti, Guid userId, DateTime expiresAt, string? revokedBy, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(jti))
                return;

            var entry = TokenBlacklistEntry.Create(jti, userId, expiresAt, revokedBy);
            await _unitOfWork.TokenBlacklist.AddAsync(entry, ct);
        }

        public Task<bool> IsTokenBlacklistedAsync(string jti, CancellationToken ct)
            => _unitOfWork.TokenBlacklist.IsBlacklistedAsync(jti, ct);

        private static string HashUserAgent(string? userAgent)
        {
            var bytes = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(userAgent) ? "unknown-device" : userAgent);
            var hash = SHA256.HashData(bytes);
            return "ua-" + Convert.ToHexString(hash)[..24].ToLowerInvariant();
        }
    }
}

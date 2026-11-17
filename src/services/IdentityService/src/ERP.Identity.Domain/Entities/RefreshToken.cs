using ERP.Identity.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Domain.Entities
{
    public class RefreshToken : BaseEntity
    {
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public string? Token { get; set; }
        public DateTime ExpiredAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? CreatedByIp { get; set; }
        public bool IsActive { get; set; }

        /// <summary>When the sign-in that started this session happened; carried through every rotation so the
        /// session's absolute limit holds (SEC-M3). Null on rows from before 2026-11-17: use <see cref="SessionStart"/>.</summary>
        public DateTime? SessionStartedAt { get; set; }

        /// <summary>The device session this token belongs to; revoking that device session ends it (SEC-M4).</summary>
        public Guid? DeviceSessionId { get; set; }

        // A row with neither date cannot prove when its session began: treat it as long over.
        public DateTime SessionStart => SessionStartedAt ?? CreatedAt ?? DateTime.MinValue;

        public RefreshToken()
        {
            IsActive = true;
        }

        public static RefreshToken Create(Guid userId, Guid tenantId, string token, DateTime expiredAt, string? createdByIp = null,
            DateTime? sessionStartedAt = null, Guid? deviceSessionId = null)
        {
            var refreshToken = new RefreshToken
            {
                UserId = userId,
                TenantId = tenantId,
                Token = token,
                ExpiredAt = expiredAt,
                CreatedByIp = createdByIp,
                IsActive = true,
                SessionStartedAt = sessionStartedAt,
                DeviceSessionId = deviceSessionId
            };
            return refreshToken;
        }

        /// <summary>Why the token stopped: a rotation (a successor exists) or a revoke (sign-out, device, account change).
        /// Only a token that was ROTATED moments ago may be presented again without counting as theft (SEC-M2).</summary>
        public RefreshTokenRevokedReason? RevokedReason { get; set; }

        public void Revoke(RefreshTokenRevokedReason reason = RefreshTokenRevokedReason.Revoked)
        {
            RevokedAt = DateTime.UtcNow;
            IsActive = false;
            RevokedReason = reason;
        }

        public bool IsTokenValid => RevokedAt == null && DateTime.UtcNow < ExpiredAt;
            
    }
}

namespace ERP.Identity.Domain.Entities
{
    public enum RefreshTokenRevokedReason : byte
    {
        /// <summary>Replaced by its successor in a refresh.</summary>
        Rotated = 1,
        /// <summary>Ended on purpose: sign-out, device revoke, account / session change, expired session.</summary>
        Revoked = 2,
    }
}

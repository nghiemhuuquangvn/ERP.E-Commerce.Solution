using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Revokes a specific JWT (by its "jti" claim) before its natural expiry — maps to
    /// the TokenBlacklist table from 001_CreateAuthDB.sql. Primary key is the JTI
    /// itself (not a surrogate Guid Id), matching the SQL table exactly.
    /// </summary>
    public class TokenBlacklistEntry
    {
        public string Jti { get; private set; } = default!;
        public Guid UserId { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public DateTime RevokedAt { get; private set; }
        public string? RevokedBy { get; private set; }

        private TokenBlacklistEntry() { }

        public static TokenBlacklistEntry Create(string jti, Guid userId, DateTime expiresAt, string? revokedBy = null)
        {
            if (string.IsNullOrWhiteSpace(jti))
                throw new ArgumentException("Jti is required", nameof(jti));

            return new TokenBlacklistEntry
            {
                Jti = jti,
                UserId = userId,
                ExpiresAt = expiresAt,
                RevokedAt = DateTime.UtcNow,
                RevokedBy = revokedBy
            };
        }
    }
}

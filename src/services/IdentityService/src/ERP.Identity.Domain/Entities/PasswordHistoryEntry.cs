using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// One previously-used password hash per user — maps to the PasswordHistories
    /// table from 001_CreateAuthDB.sql, used to enforce the "can't reuse your last N
    /// passwords" policy on change/reset.
    /// </summary>
    public class PasswordHistoryEntry
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public string PasswordHash { get; private set; } = default!;
        public DateTime CreatedAt { get; private set; }

        private PasswordHistoryEntry() { }

        public static PasswordHistoryEntry Create(Guid userId, string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("PasswordHash is required", nameof(passwordHash));

            return new PasswordHistoryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}

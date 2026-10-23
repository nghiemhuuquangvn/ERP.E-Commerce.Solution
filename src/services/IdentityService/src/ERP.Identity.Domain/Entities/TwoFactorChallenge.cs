using ERP.Identity.Domain.Common;
using ERP.Identity.Domain.Events;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// A one-time 2FA code challenge issued during login. The plaintext code is never
    /// persisted — only its hash (via IPasswordHasher, injected as a verify delegate from
    /// the Infrastructure layer so Domain stays free of Application-layer references).
    /// </summary>
    public class TwoFactorChallenge : BaseEntity
    {
        public Guid UserId { get; private set; }
        public Guid TenantId { get; private set; }
        public string CodeHash { get; private set; } = default!;
        public string Method { get; private set; } = default!; // "Sms" | "Email"
        public string Destination { get; private set; } = default!; // MASKED phone/email (display only; never used for delivery)
        public DateTime ExpiredAt { get; private set; }
        public int AttemptCount { get; private set; }
        public int MaxAttempts { get; private set; }
        public DateTime? VerifiedAt { get; private set; }

        protected TwoFactorChallenge() { }

        public static TwoFactorChallenge Create(
            Guid userId,
            Guid tenantId,
            string codeHash,
            string method,
            string destination,
            string recipient,
            int expiryMinutes,
            int maxAttempts,
            string plaintextCode)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("UserId is required", nameof(userId));
            if (string.IsNullOrWhiteSpace(codeHash))
                throw new ArgumentException("CodeHash is required", nameof(codeHash));
            if (string.IsNullOrWhiteSpace(method))
                throw new ArgumentException("Method is required", nameof(method));
            if (string.IsNullOrWhiteSpace(recipient))
                throw new ArgumentException("Recipient is required: the code must be delivered somewhere", nameof(recipient));

            var challenge = new TwoFactorChallenge
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TenantId = tenantId,
                CodeHash = codeHash,
                Method = method,
                Destination = destination,
                ExpiredAt = DateTime.UtcNow.AddMinutes(expiryMinutes),
                AttemptCount = 0,
                MaxAttempts = maxAttempts,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };

            // The event is what NotificationService delivers: it needs the REAL address. The masked value above is
            // what the entity keeps and what clients may be shown (it used to be sent here too, so the code was
            // "delivered" to j***@example.com and never arrived).
            challenge.AddDomainEvent(new TwoFactorCodeGeneratedDomainEvent(
                userId, tenantId, method, recipient, plaintextCode, expiryMinutes));

            return challenge;
        }

        /// <summary>
        /// Verifies the supplied plaintext code against <see cref="CodeHash"/> using the
        /// given hash-verification function (typically IPasswordHasher.VerifyPassword).
        /// Mutates AttemptCount on mismatch — caller must persist afterwards regardless of result.
        /// </summary>
        public bool VerifyCode(Func<string, string, bool> verifyHash, string code)
        {
            if (VerifiedAt != null || DateTime.UtcNow >= ExpiredAt || AttemptCount >= MaxAttempts)
                return false;

            var isMatch = verifyHash(CodeHash, code);
            if (!isMatch)
                AttemptCount++;

            return isMatch;
        }

        public void MarkVerified()
        {
            VerifiedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Ends this challenge because a newer one replaced it: its code can no longer be used. Without this every
        /// resend left the earlier codes valid, each with its own set of attempts (more guesses for an attacker).
        /// </summary>
        public void Supersede()
        {
            if (VerifiedAt == null && DateTime.UtcNow < ExpiredAt)
                ExpiredAt = DateTime.UtcNow;
        }

        public bool IsVerified => VerifiedAt != null;
    }
}

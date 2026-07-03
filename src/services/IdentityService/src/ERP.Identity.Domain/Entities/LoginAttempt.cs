using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// One row per login attempt (success or failure), keyed by IP/email for the
    /// IP-based rate-limiting the LoginAttempts table's own SQL comment describes.
    /// Not a BaseEntity — same immutable-append-only reasoning as SecurityAuditLog.
    /// </summary>
    public class LoginAttempt
    {
        public Guid Id { get; private set; }
        public string IpAddress { get; private set; } = default!;
        public string? Email { get; private set; }
        public Guid? UserId { get; private set; }
        public bool IsSuccessful { get; private set; }
        public string? FailureReason { get; private set; }
        public DateTime AttemptedAt { get; private set; }

        private LoginAttempt() { }

        public static LoginAttempt Create(string ipAddress, bool isSuccessful, string? email = null, Guid? userId = null, string? failureReason = null)
        {
            return new LoginAttempt
            {
                Id = Guid.NewGuid(),
                IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : ipAddress,
                Email = email,
                UserId = userId,
                IsSuccessful = isSuccessful,
                FailureReason = failureReason,
                AttemptedAt = DateTime.UtcNow
            };
        }
    }
}

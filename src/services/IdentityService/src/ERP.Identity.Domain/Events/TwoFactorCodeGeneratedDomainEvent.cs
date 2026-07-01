using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Events
{
    /// <summary>
    /// Raised when a new 2FA challenge code is generated during login/resend.
    /// Carries the PLAINTEXT code (needed for delivery via Notification) even though
    /// TwoFactorChallenge.CodeHash only ever persists the hash — the plaintext exists
    /// transiently in memory plus this one outbound Kafka message.
    /// </summary>
    public class TwoFactorCodeGeneratedDomainEvent : IDomainEvent
    {
        public Guid UserId { get; }
        public Guid TenantId { get; }
        public string Method { get; }
        public string Destination { get; }
        public string Code { get; }
        public int ExpiryMinutes { get; }
        public DateTime OccurredOn { get; }

        public TwoFactorCodeGeneratedDomainEvent(Guid userId, Guid tenantId, string method, string destination, string code, int expiryMinutes)
        {
            UserId = userId;
            TenantId = tenantId;
            Method = method;
            Destination = destination;
            Code = code;
            ExpiryMinutes = expiryMinutes;
            OccurredOn = DateTime.UtcNow;
        }
    }
}

using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Events
{
    /// <summary>
    /// Raised when a user requests a password reset. Carries the one-time reset LINK (which
    /// embeds the plaintext token) for delivery by NotificationService — same transient
    /// plaintext-in-one-message model as <see cref="TwoFactorCodeGeneratedDomainEvent"/>;
    /// PasswordResetToken itself only ever persists the hash. Excluded from tenant webhooks.
    /// </summary>
    public class PasswordResetRequestedDomainEvent : IDomainEvent
    {
        public Guid UserId { get; }
        public Guid TenantId { get; }
        public string Email { get; }
        public string ResetLink { get; }
        public int ExpiryMinutes { get; }
        public DateTime OccurredOn { get; }

        public PasswordResetRequestedDomainEvent(Guid userId, Guid tenantId, string email, string resetLink, int expiryMinutes)
        {
            UserId = userId;
            TenantId = tenantId;
            Email = email;
            ResetLink = resetLink;
            ExpiryMinutes = expiryMinutes;
            OccurredOn = DateTime.UtcNow;
        }
    }
}

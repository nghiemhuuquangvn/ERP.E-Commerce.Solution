using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Events
{
    /// <summary>
    /// Raised when an email-confirmation token is issued (self-service registration).
    /// Carries the one-time confirmation LINK for delivery by NotificationService;
    /// EmailConfirmToken itself only persists the token hash. Excluded from tenant webhooks.
    /// </summary>
    public class EmailConfirmationRequestedDomainEvent : IDomainEvent
    {
        public Guid UserId { get; }
        public Guid TenantId { get; }
        public string Email { get; }
        public string ConfirmLink { get; }
        public int ExpiryHours { get; }
        public DateTime OccurredOn { get; }

        public EmailConfirmationRequestedDomainEvent(Guid userId, Guid tenantId, string email, string confirmLink, int expiryHours)
        {
            UserId = userId;
            TenantId = tenantId;
            Email = email;
            ConfirmLink = confirmLink;
            ExpiryHours = expiryHours;
            OccurredOn = DateTime.UtcNow;
        }
    }
}

using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Events
{
    public class UserUpdatedDomainEvent : IDomainEvent
    {
        public Guid UserId { get; }
        public Guid TenantId { get; }
        public string? FullName { get; }
        public string? UserName { get; }
        public DateTime OccurredOn { get; }

        public UserUpdatedDomainEvent(Guid userId, Guid tenantId, string? fullName, string? userName)
        {
            UserId = userId;
            TenantId = tenantId;
            FullName = fullName;
            UserName = userName;
            OccurredOn = DateTime.UtcNow;
        }
    }
}

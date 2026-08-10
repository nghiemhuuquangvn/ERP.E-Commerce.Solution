using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Events
{
    public class RoleAssignedDomainEvent : IDomainEvent
    {
        public Guid UserId { get; }
        public Guid TenantId { get; }
        public Guid RoleId { get; }
        public string RoleCode { get; }
        public DateTime OccurredOn { get; }

        // Null when the role was assigned by the system itself (e.g. self-registration's
        // default-role assignment) rather than by another user — NotificationService uses
        // this to suppress a "your role was changed by nobody" notification in that case.
        public Guid? ChangedByUserId { get; }
        public string? ChangedByName { get; }

        public RoleAssignedDomainEvent(Guid userId, Guid tenantId, Guid roleId, string roleCode, Guid? changedByUserId = null, string? changedByName = null)
        {
            UserId = userId;
            TenantId = tenantId;
            RoleId = roleId;
            RoleCode = roleCode;
            OccurredOn = DateTime.UtcNow;
            ChangedByUserId = changedByUserId;
            ChangedByName = changedByName;
        }
    }
}

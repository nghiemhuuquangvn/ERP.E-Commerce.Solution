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

        public RoleAssignedDomainEvent(Guid userId, Guid tenantId, Guid roleId, string roleCode)
        {
            UserId = userId;
            TenantId = tenantId;
            RoleId = roleId;
            RoleCode = roleCode;
            OccurredOn = DateTime.UtcNow;
        }
    }
}

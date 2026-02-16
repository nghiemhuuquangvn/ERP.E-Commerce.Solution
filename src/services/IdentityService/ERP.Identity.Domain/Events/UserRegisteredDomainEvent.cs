using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ERP.Identity.Domain.Common;


namespace ERP.Identity.Domain.Events
{
    public class UserRegisteredDomainEvent : IDomainEvent
    {

        public Guid UserId { get; }
        public Guid TenantId { get; }
        public string Email { get; }
        public DateTime OccurredOn { get; } = DateTime.UtcNow;

        public UserRegisteredDomainEvent(Guid userId, Guid tenantId, string email)
        {
            UserId = userId;
            TenantId = tenantId;
            Email = email;
        }
    }
}

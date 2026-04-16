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
        public string UserType { get; } = "Customer";
        public DateTime OccurredOn { get; }

        public UserRegisteredDomainEvent(Guid userId, Guid tenantId, string email, string userType = "Customer")
        {
            UserId = userId;
            TenantId = tenantId;
            Email = email;
            UserType = userType;
            OccurredOn = DateTime.UtcNow;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Domain.Entities
{
    public class AuthUserTenant
    {
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public bool IsActive { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        protected AuthUserTenant() { }
        internal AuthUserTenant(Guid userId, Guid tenantId)
        {
            UserId = userId;
            TenantId = tenantId;
            IsActive = true;
        }
    }

}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Domain.Entities
{
    public class UserRole
    {
        public Guid UserId { get; set; }
        public Guid RoleId { get; set; }
        public Guid TenantId { get; set; }
        public virtual Role Role { get; set; } = default!;
        public virtual User User { get; set; } = default!;
        protected UserRole() { }
        internal UserRole(Guid userId, Guid roleId,  Guid tenantId)
        {
            UserId = userId;
            RoleId = roleId;
            TenantId = tenantId;
        }
        
    }
}

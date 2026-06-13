using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Domain.Entities
{
    public class UserPermission
    {
        public Guid UserId { get; set; }
        public Guid PermissionId { get; set; }
        public Guid TenantId { get; set; }
        public bool IsGranted { get; set; } // true: grant, false: deny
        public virtual Permission Permission { get; set; } = new Permission();
        public virtual User User { get; set; } = new User();
        
    }
}

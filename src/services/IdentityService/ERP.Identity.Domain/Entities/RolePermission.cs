using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Domain.Entities
{
    public class RolePermission
    {
        public Guid RoleId { get; set; }
        public Guid PermissionId { get; set; }
        public Guid TenantId { get; set; }
        public Permission Permission { get; set; } = default!;
        public Role Role { get; set; } = default!;
    }
}

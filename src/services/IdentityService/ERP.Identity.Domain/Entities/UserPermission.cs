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
        public bool IsGranted { get; set; }
        public Guid TenantId { get; set; }
    }
}

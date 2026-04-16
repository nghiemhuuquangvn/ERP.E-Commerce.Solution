using ERP.Identity.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Domain.Entities
{
    public class Permission : BaseEntity
    {
        public string PermissionCode { get; set; } = default!;
        public string? PermissionName { get; set; }
        public string? Module { get; set; }

        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
        
    }

}

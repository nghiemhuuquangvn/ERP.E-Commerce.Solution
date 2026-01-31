using ERP.Identity.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Domain.Entities
{
    public class Role : BaseEntity
    {
        public string RoleCode { get; set; } = default!;
        public string RoleName { get; set; } = default!;
        public Guid TenantId { get; set; }
    }

}

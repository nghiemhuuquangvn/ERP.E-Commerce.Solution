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
        public bool IsDefault { get; set; }
    }

}

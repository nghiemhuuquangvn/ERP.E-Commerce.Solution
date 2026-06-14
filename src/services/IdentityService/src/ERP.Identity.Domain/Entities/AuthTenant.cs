using ERP.Identity.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Domain.Entities
{
    public class AuthTenant : BaseEntity
    {
        public string TenantCode { get; set; } = default!;
        public string TenantName { get; set; } = default!;
        public byte Status { get; set; } = 1; // 1: Active, 0: Inactive
        public virtual ICollection<AuthUserTenant> UserTenants { get; set; } = default!;

        public AuthTenant() { }

        public static AuthTenant Create(string code, string name)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Tenant code is required");
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Tenant name is required");

            return new AuthTenant
            {
                Id = Guid.NewGuid(),
                TenantCode = code.ToUpperInvariant(),
                TenantName = name,
                Status = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };
        }

        public void Activate()
        {
            Status = 1;
        }

        public void Deactivate()
        {
            Status = 0;
        }

        public bool IsActive => Status == 1;
    }

}

using ERP.Identity.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Domain.Entities
{
    public class ExternalLogin : BaseEntity
    {
        public Guid UserId { get; set; }
        public string? Provider { get; set; } = default!;
        public string? ProviderKey { get; set; } = default!;
        public string? Email { get; set; } = default!;
        public Guid TenantId { get; set; }
        public bool IsActive { get; set; } = true;

        protected ExternalLogin() { }
        private ExternalLogin(Guid userId, Guid tenantId, string provider, string providerKey, string email)
        {
            UserId = userId;
            TenantId = tenantId;
            Provider = provider;
            ProviderKey = providerKey;
            Email = email;
            CreatedBy = "System";
            CreatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// The link belongs to the tenant it was made in: lookups filter by (provider, key, tenant), so a link
        /// created with an empty tenant id could never be found again and the next login would create a duplicate user.
        /// </summary>
        public static ExternalLogin Create(Guid userId, Guid tenantId, string provider, string providerKey, string email)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required", nameof(tenantId));

            return new ExternalLogin(userId, tenantId, provider, providerKey, email);
        }

        public void Disable() => IsActive = false;
    }
}

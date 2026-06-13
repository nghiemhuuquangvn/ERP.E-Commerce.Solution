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
        private ExternalLogin(Guid userId, string provider, string providerKey, string email)
        {
            UserId = userId;
            Provider = provider;
            ProviderKey = providerKey;
            Email = email;
        }

        public static ExternalLogin Create(Guid userId, string provider, string providerKey, string email)
        {
            return new ExternalLogin(userId, provider, providerKey, email);
        }

        public void Disable() => IsActive = false;
    }
}

using ERP.Identity.Domain.Common;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// A custom host name (e.g. "shop.acme.com") that belongs to exactly one tenant. Requests
    /// arriving on that host are served as that tenant — the tenant is decided by the URL,
    /// never by anything the client sends. Subdomains of Tenancy:BaseDomain
    /// ("acme.erp.com") resolve by TenantCode without a row here.
    /// </summary>
    public class TenantDomain : BaseEntity
    {
        public Guid TenantId { get; private set; }
        public string Host { get; private set; } = default!;
        public bool IsPrimary { get; private set; }

        protected TenantDomain() { }

        public static TenantDomain Create(Guid tenantId, string host, bool isPrimary)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("Tenant is required", nameof(tenantId));

            var normalized = NormalizeHost(host);
            if (string.IsNullOrEmpty(normalized) || !normalized.Contains('.') || Uri.CheckHostName(normalized) != UriHostNameType.Dns)
                throw new ArgumentException("A fully qualified DNS host name is required", nameof(host));

            return new TenantDomain
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Host = normalized,
                IsPrimary = isPrimary,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };
        }

        /// <summary>Lower-case, no port, no trailing dot — the only form ever stored or compared.</summary>
        public static string NormalizeHost(string? host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return string.Empty;

            var value = host.Trim().ToLowerInvariant();
            var portIndex = value.LastIndexOf(':');
            if (portIndex > 0 && !value.Contains(']') && value.IndexOf(':') == portIndex)
                value = value[..portIndex];
            return value.TrimEnd('.');
        }
    }
}

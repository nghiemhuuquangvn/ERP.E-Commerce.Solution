using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Per-tenant File Storage configuration. One row per AuthTenant (TenantId is unique).
    /// Storage+CRUD only — no IFileStorageService exists yet to read Provider/CdnBaseUrl from.
    /// </summary>
    public class TenantFileStorageSettings : BaseEntity
    {
        public Guid TenantId { get; private set; }
        public string Provider { get; private set; } = "Local";
        public int MaxUploadSizeMb { get; private set; } = 10;
        public string? CdnBaseUrl { get; private set; }

        protected TenantFileStorageSettings() { }

        public static TenantFileStorageSettings CreateDefault(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required", nameof(tenantId));

            return new TenantFileStorageSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Provider = "Local",
                MaxUploadSizeMb = 10,
                CdnBaseUrl = null,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(string provider, int maxUploadSizeMb, string? cdnBaseUrl)
        {
            Provider = provider;
            MaxUploadSizeMb = maxUploadSizeMb;
            CdnBaseUrl = cdnBaseUrl;
        }
    }
}

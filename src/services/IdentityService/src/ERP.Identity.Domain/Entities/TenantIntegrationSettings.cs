using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Per-tenant Integration configuration. One row per AuthTenant (TenantId is unique).
    /// ExternalApiKeyEncrypted must already be encrypted by the caller (ISecretProtector.Protect)
    /// — this entity never sees or stores the plaintext key itself, same pattern as
    /// PaymentService's TenantPaymentSettings.VnPaySecretKeyEncrypted.
    /// </summary>
    public class TenantIntegrationSettings : BaseEntity
    {
        public Guid TenantId { get; private set; }
        public string? ExternalApiBaseUrl { get; private set; }
        public string? ExternalApiKeyEncrypted { get; private set; }
        public bool SyncEnabled { get; private set; } = false;

        protected TenantIntegrationSettings() { }

        public static TenantIntegrationSettings CreateDefault(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required", nameof(tenantId));

            return new TenantIntegrationSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SyncEnabled = false,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>externalApiKeyEncrypted: null means "leave the currently stored key unchanged".</summary>
        public void Update(string? externalApiBaseUrl, string? externalApiKeyEncrypted, bool syncEnabled)
        {
            ExternalApiBaseUrl = externalApiBaseUrl;
            if (externalApiKeyEncrypted != null)
                ExternalApiKeyEncrypted = externalApiKeyEncrypted;
            SyncEnabled = syncEnabled;
        }
    }
}

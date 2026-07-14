using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Single well-known-GUID row holding the platform-wide external-integrations switch.
    /// Distinct from NotificationService's WebhookEndpoint (outbound webhook delivery) —
    /// this is third-party credential/endpoint config, not webhook delivery.
    /// </summary>
    public class SystemIntegrationSettings : BaseEntity
    {
        public static readonly Guid WellKnownId = Guid.Parse("E4000000-0000-0000-0000-000000000001");

        public bool ExternalIntegrationsAllowed { get; private set; } = true;

        protected SystemIntegrationSettings() { }

        public static SystemIntegrationSettings CreateDefault()
        {
            return new SystemIntegrationSettings
            {
                Id = WellKnownId,
                ExternalIntegrationsAllowed = true,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(bool externalIntegrationsAllowed)
        {
            ExternalIntegrationsAllowed = externalIntegrationsAllowed;
        }
    }
}

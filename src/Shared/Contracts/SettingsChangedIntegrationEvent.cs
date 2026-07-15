namespace ERP.Shared.Contracts
{
    /// <summary>
    /// Published by a service whenever any Settings category (System or Tenant tier) is
    /// updated. Deliberately does NOT carry the changed field values — a consumer that
    /// cares re-fetches the category's current value via that service's own Settings API,
    /// so it never needs to understand another service's Settings schema.
    /// </summary>
    public class SettingsChangedIntegrationEvent : IntegrationEventBase
    {
        /// <summary>Publishing service, e.g. "IdentityService", "SharedMasterDataService".</summary>
        public string ServiceName { get; set; } = default!;

        /// <summary>Settings category, e.g. "Auth", "Inventory", "Currency".</summary>
        public string Category { get; set; } = default!;

        /// <summary>"System" or "Tenant".</summary>
        public string Scope { get; set; } = default!;

        /// <summary>Set only when Scope is "Tenant".</summary>
        public Guid? TenantId { get; set; }
    }
}

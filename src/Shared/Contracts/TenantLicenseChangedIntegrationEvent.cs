namespace ERP.Shared.Contracts
{
    /// <summary>
    /// Published by IdentityService whenever a tenant's license is issued, renewed,
    /// suspended, or revoked. Consumed by NotificationService (alert the tenant admin)
    /// and ReportingService (analytics). Deliberately does not carry the module list —
    /// a consumer that needs it calls IdentityService's license API.
    /// </summary>
    public class TenantLicenseChangedIntegrationEvent : IntegrationEventBase
    {
        public Guid TenantId { get; set; }

        /// <summary>"Issued" | "Renewed" | "Suspended" | "Revoked".</summary>
        public string Action { get; set; } = default!;
    }
}

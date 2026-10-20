using ERP.Shared.Core.Domain;
using ERP.Shared.Contracts;

namespace ERP.SO.Domain.Events
{
    public class SalesOrderShippedEvent : IntegrationEventBase, ITenantEvent
    {
        /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
        public Guid TenantId { get; set; }

        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public string TrackingNumber { get; set; } = string.Empty;
        public string Carrier { get; set; } = string.Empty;
        public DateTime ShippedAt { get; set; }
    }
}

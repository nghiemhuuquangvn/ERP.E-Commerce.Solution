using ERP.Shared.Core.Domain;
using ERP.Shared.Contracts;

namespace ERP.SO.Domain.Events
{
    public class SalesOrderCancelledEvent : IntegrationEventBase, ITenantEvent
    {
        /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
        public Guid TenantId { get; set; }

        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public string CancellationReason { get; set; } = string.Empty;
        public DateTime CancelledAt { get; set; }
    }
}

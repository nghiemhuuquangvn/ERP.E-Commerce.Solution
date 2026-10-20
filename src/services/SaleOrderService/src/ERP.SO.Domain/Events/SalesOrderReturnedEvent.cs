using ERP.Shared.Core.Domain;
using ERP.Shared.Contracts;

namespace ERP.SO.Domain.Events
{
    /// <summary>
    /// Raised when a return is completed (CompleteReturn()). Payment/Accounting react
    /// by issuing the actual refund and reversing the journal entry; Warehouse reacts
    /// by restocking. SaleOrderService's own job here is only to record that the
    /// return happened — it does not process the refund itself.
    /// </summary>
    public class SalesOrderReturnedEvent : IntegrationEventBase, ITenantEvent
    {
        /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
        public Guid TenantId { get; set; }

        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public decimal RefundAmount { get; set; }
        public DateTime ReturnedAt { get; set; }
    }
}

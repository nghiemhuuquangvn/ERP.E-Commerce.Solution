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
        /// <summary>The refund includes the delivery fee the customer paid (ROADMAP P1-11c); false = goods only.</summary>
        public bool ShippingFeeRefunded { get; set; }
        public DateTime ReturnedAt { get; set; }

        /// <summary>The goods that come back: every line that was shipped (a return is of the whole order). Warehouse puts them back on the shelf (ROADMAP P1-5).</summary>
        public List<ReturnedLine> Lines { get; set; } = new();

        public class ReturnedLine
        {
            public Guid ProductId { get; set; }
            public int Quantity { get; set; }
        }
    }
}

using ERP.Shared.Contracts;
using ERP.Shared.Core.Domain;

namespace ERP.SO.Domain.Events
{
    /// <summary>
    /// Raised when a Draft sales order is submitted: it now waits for approval. NotificationService tells the holders of
    /// <c>sales_order:approve</c> (bell, and push when it is switched on). Published on the sales order exchange with the routing key
    /// <c>sales_order.SalesOrderSubmittedEvent</c>.
    /// </summary>
    public class SalesOrderSubmittedEvent : IntegrationEventBase, ITenantEvent
    {
        /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
        public Guid TenantId { get; set; }

        public Guid OrderId { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
    }
}

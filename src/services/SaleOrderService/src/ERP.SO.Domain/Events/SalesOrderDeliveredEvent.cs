using ERP.Shared.Contracts;

namespace ERP.SO.Domain.Events
{
    /// <summary>
    /// Previously did not exist at all — MarkAsDeliveredCommandHandler already looped
    /// over order.DomainEvents and published them, but MarkAsDelivered() never raised
    /// anything, so that loop always published zero events. Meant to trigger loyalty
    /// points (Promotion) and CRM purchase-history updates per the business flow docs.
    /// </summary>
    public class SalesOrderDeliveredEvent : IntegrationEventBase
    {
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime DeliveredAt { get; set; }
    }
}

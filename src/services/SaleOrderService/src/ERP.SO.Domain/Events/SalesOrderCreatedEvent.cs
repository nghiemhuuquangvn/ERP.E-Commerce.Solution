using ERP.Shared.Core.Domain;
using ERP.Shared.Contracts;
using ERP.SO.Domain.Entities;

namespace ERP.SO.Domain.Events
{
    public class SalesOrderCreatedEvent : IntegrationEventBase, ITenantEvent
    {
        /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
        public Guid TenantId { get; set; }

        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        /// <summary>Where order notifications go (ROADMAP P1-6); empty on events published before it.</summary>
        public string CustomerEmail { get; set; } = string.Empty;
        public string OrderNo { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        /// <summary>What the customer owes: the goods after the coupon plus ShippingFee.</summary>
        public decimal TotalAmount { get; set; }
        /// <summary>Delivery charged to the customer, included in TotalAmount (P1-11b); 0 on events published before it. The goods are TotalAmount - ShippingFee.</summary>
        public decimal ShippingFee { get; set; }
        public decimal DiscountAmount { get; set; }
        public List<LineItem> LineItems { get; set; } = new();
        public OrderStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public class LineItem
        {
            public Guid ProductId { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
        }
    }
}

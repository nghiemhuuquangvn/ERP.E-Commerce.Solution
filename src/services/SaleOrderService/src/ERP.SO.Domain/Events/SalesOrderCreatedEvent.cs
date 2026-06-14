using ERP.Shared.Contracts;

namespace ERP.SO.Domain.Events
{
    public class SalesOrderCreatedEvent : IntegrationEventBase
    {
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
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

using ERP.Shared.Contracts;

namespace ERP.SO.Domain.Events
{
    /// <summary>Raised when some but not all line items on an order have shipped.</summary>
    public class SalesOrderPartiallyShippedEvent : IntegrationEventBase
    {
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public string TrackingNumber { get; set; } = string.Empty;
        public string Carrier { get; set; } = string.Empty;
        public List<ShippedLine> ShippedLines { get; set; } = new();
        public DateTime ShippedAt { get; set; }

        public class ShippedLine
        {
            public Guid ProductId { get; set; }
            public int Quantity { get; set; }
        }
    }
}

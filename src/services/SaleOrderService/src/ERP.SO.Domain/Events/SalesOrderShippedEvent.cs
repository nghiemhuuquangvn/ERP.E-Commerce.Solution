using ERP.Shared.Contracts;

namespace ERP.SO.Domain.Events
{
    public class SalesOrderShippedEvent : IntegrationEventBase
    {
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public string TrackingNumber { get; set; } = string.Empty;
        public string Carrier { get; set; } = string.Empty;
        public DateTime ShippedAt { get; set; }
    }
}

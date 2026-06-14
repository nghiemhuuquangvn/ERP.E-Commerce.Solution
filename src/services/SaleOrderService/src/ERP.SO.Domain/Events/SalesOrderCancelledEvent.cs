using ERP.Shared.Contracts;

namespace ERP.SO.Domain.Events
{
    public class SalesOrderCancelledEvent : IntegrationEventBase
    {
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public string CancellationReason { get; set; } = string.Empty;
        public DateTime CancelledAt { get; set; }
    }
}

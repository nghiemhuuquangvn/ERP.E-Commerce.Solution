using ERP.Shared.Contracts;

namespace ERP.SO.Domain.Events
{
    public class SalesOrderApprovedEvent : IntegrationEventBase
    {
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public DateTime ApprovedAt { get; set; }
        public decimal TotalAmount { get; set; }
    }
}

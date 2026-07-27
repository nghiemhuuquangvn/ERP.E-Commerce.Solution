using ERP.Shared.Contracts;

namespace ERP.SO.Domain.Events
{
    public class SalesQuotationCreatedEvent : IntegrationEventBase
    {
        public Guid QuotationId { get; set; }
        public Guid CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Events
{
    /// <summary>
    /// Cross-service integration event (Outbox+Kafka, product-topic) so SalesOrder/POS/
    /// Promotion can refresh their local product caches — previously missing entirely,
    /// only ProductCreated/Published/Discontinued existed.
    /// </summary>
    public class ProductUpdatedDomainEvent : IDomainEvent
    {
        public Guid ProductId { get; }
        public string Name { get; }
        public string Sku { get; }
        public DateTime OccurredOn { get; }

        public ProductUpdatedDomainEvent(Guid productId, string name, string sku)
        {
            ProductId = productId;
            Name = name;
            Sku = sku;
            OccurredOn = DateTime.UtcNow;
        }
    }
}

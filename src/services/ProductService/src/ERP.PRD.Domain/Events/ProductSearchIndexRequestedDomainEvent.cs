using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Events
{
    /// <summary>
    /// Raised whenever a Product/ProductVariant/ProductAttributeValue change means the
    /// derived Elasticsearch "products" search document for the given Product is stale
    /// and needs to be rebuilt. Handled specially by ProductDbContext.SaveChangesAsync:
    /// instead of going through OutboxMessages/Kafka, it upserts/bumps a row in
    /// SearchIndexQueue (deduplicated by ProductId), which SearchIndexProcessor polls.
    /// </summary>
    public class ProductSearchIndexRequestedDomainEvent : IDomainEvent
    {
        public Guid ProductId { get; }
        public DateTime OccurredOn { get; }

        public ProductSearchIndexRequestedDomainEvent(Guid productId)
        {
            ProductId = productId;
            OccurredOn = DateTime.UtcNow;
        }
    }
}

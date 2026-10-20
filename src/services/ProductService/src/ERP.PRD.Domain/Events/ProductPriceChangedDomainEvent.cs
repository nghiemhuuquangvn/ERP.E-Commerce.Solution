using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Events
{
    /// <summary>
    /// Cross-service integration event (Outbox+Kafka, product-topic) so PromotionService/
    /// POSService can recompute discounts/refresh their price cache — confirmed absent
    /// from the entire ProductService domain before this; POS's and Promotion's
    /// consumers (wired earlier this session) had to default price to 0 because this
    /// event didn't exist yet.
    /// </summary>
    public class ProductPriceChangedDomainEvent : IDomainEvent, ITenantEvent
    {
        /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
        public Guid TenantId { get; set; }

        public Guid ProductId { get; }
        public Guid VariantId { get; }
        public decimal OldPrice { get; }
        public decimal NewPrice { get; }
        public DateTime OccurredOn { get; }

        public ProductPriceChangedDomainEvent(Guid productId, Guid variantId, decimal oldPrice, decimal newPrice)
        {
            ProductId = productId;
            VariantId = variantId;
            OldPrice = oldPrice;
            NewPrice = newPrice;
            OccurredOn = DateTime.UtcNow;
        }
    }
}

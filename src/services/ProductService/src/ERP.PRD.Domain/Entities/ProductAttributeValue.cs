using ERP.Shared.Core.Domain;
using ERP.PRD.Domain.Enums;
using ERP.PRD.Domain.Events;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// EAV value row for a <see cref="ProductAttribute"/>, owned by either a Product or a
    /// ProductVariant (see <see cref="OwnerType"/>/<see cref="OwnerId"/>). Standalone entity —
    /// not nested inside Product's/ProductVariant's own child collections — queried directly
    /// via its own DbSet, the same pattern already used for PriceListItem.
    /// Exactly one of ValueText/ValueNumber/ValueBoolean/ValueDate/ProductAttributeOptionId/
    /// ValueOptionIdsJson is populated, matching the owning ProductAttribute's DataType.
    /// </summary>
    public class ProductAttributeValue : BaseEntity
    {
        public Guid ProductAttributeId { get; set; }
        public AttributeOwnerType OwnerType { get; set; }
        public Guid OwnerId { get; set; }
        public string? ValueText { get; set; }
        public decimal? ValueNumber { get; set; }
        public bool? ValueBoolean { get; set; }
        public DateTime? ValueDate { get; set; }
        public Guid? ProductAttributeOptionId { get; set; }
        public string? ValueOptionIdsJson { get; set; }

        public static ProductAttributeValue Create(Guid attributeId, AttributeOwnerType ownerType, Guid ownerId) =>
            new()
            {
                Id = Guid.NewGuid(),
                ProductAttributeId = attributeId,
                OwnerType = ownerType,
                OwnerId = ownerId,
                CreatedAt = DateTime.UtcNow
            };

        /// <summary>
        /// Marks the owning Product (resolved by the caller — this value may be owned by a
        /// Product or a ProductVariant) as needing its Elasticsearch search document rebuilt.
        /// Public because Application-layer handlers manipulate ProductAttributeValue directly
        /// via its DbSet, and BaseEntity.AddDomainEvent is protected.
        /// </summary>
        public void RequestSearchReindex(Guid productId)
        {
            AddDomainEvent(new ProductSearchIndexRequestedDomainEvent(productId));
        }
    }
}

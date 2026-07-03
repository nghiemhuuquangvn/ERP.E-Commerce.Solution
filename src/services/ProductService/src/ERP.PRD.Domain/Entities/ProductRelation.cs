using ERP.PRD.Domain.Enums;
using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// A directed cross-sell/upsell link from one Product to another (e.g. "customers
    /// who bought this also bought..." or "consider this premium alternative"). Directed
    /// on purpose — A recommending B doesn't imply B recommends A.
    /// </summary>
    public class ProductRelation : BaseEntity
    {
        public Guid ProductId { get; private set; }
        public Guid RelatedProductId { get; private set; }
        public ProductRelationType RelationType { get; private set; }
        public int SortOrder { get; private set; }

        private ProductRelation() { }

        public static ProductRelation Create(Guid productId, Guid relatedProductId, ProductRelationType relationType, int sortOrder = 0)
        {
            if (productId == relatedProductId)
                throw new ArgumentException("A product cannot relate to itself");

            return new ProductRelation
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                RelatedProductId = relatedProductId,
                RelationType = relationType,
                SortOrder = sortOrder,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}

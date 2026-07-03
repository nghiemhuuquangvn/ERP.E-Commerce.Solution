using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// One component line of a bundle/combo product (ProductType.Bundle) — e.g. a
    /// "Camera Kit" bundle Product referencing the Camera Body and Lens Products with
    /// quantities. The bundle itself is a normal Product row; this table only records
    /// its composition, so it's still sellable/priced/searched exactly like any other
    /// product.
    /// </summary>
    public class ProductBundleItem : BaseEntity
    {
        public Guid BundleProductId { get; private set; }
        public Guid ComponentProductId { get; private set; }
        public int Quantity { get; private set; }

        private ProductBundleItem() { }

        public static ProductBundleItem Create(Guid bundleProductId, Guid componentProductId, int quantity)
        {
            if (bundleProductId == componentProductId)
                throw new ArgumentException("A bundle cannot contain itself");
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be greater than zero");

            return new ProductBundleItem
            {
                Id = Guid.NewGuid(),
                BundleProductId = bundleProductId,
                ComponentProductId = componentProductId,
                Quantity = quantity,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void UpdateQuantity(int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be greater than zero");

            Quantity = quantity;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

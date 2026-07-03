using ERP.Shared.Core.Domain;
using ERP.PRD.Domain.Events;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// Product variant representing a specific SKU/barcode combination.
    /// Each variant has its own inventory tracking and barcode.
    /// </summary>
    public class ProductVariant : BaseEntity
    {
        public Guid ProductId { get; set; }
        public string Sku { get; set; } = string.Empty; // Globally unique
        public string? Barcode { get; set; }
        public decimal Cost { get; set; }
        public decimal? Weight { get; set; }
        public string? Dimensions { get; set; }

        // Structured physical dimensions/weight (additive; legacy free-text
        // Dimensions column above is kept as-is for backward compatibility)
        public string? WeightUnit { get; set; } = "kg";
        public decimal? Length { get; set; }
        public decimal? Width { get; set; }
        public decimal? Height { get; set; }
        public string? DimensionUnit { get; set; } = "cm";

        // Opaque escape hatch for arbitrary structured data (never queried via SQL JSON functions)
        public string? ExtendedDataJson { get; set; }

        public ProductVariant() { }

        public ProductVariant(string sku, string? barcode = null, decimal cost = 0)
        {
            Id = Guid.NewGuid();
            Sku = sku;
            Barcode = barcode;
            Cost = cost;
            CreatedAt = DateTime.UtcNow;
        }

        public void SetDimensions(decimal? length, decimal? width, decimal? height, string unit = "cm")
        {
            if (length is < 0 || width is < 0 || height is < 0)
                throw new ArgumentException("Dimensions cannot be negative");

            Length = length;
            Width = width;
            Height = height;
            DimensionUnit = unit;
            UpdatedAt = DateTime.UtcNow;
            AddDomainEvent(new ProductSearchIndexRequestedDomainEvent(ProductId));
        }

        public void SetWeight(decimal weight, string unit = "kg")
        {
            if (weight < 0)
                throw new ArgumentException("Weight cannot be negative");

            Weight = weight;
            WeightUnit = unit;
            UpdatedAt = DateTime.UtcNow;
            AddDomainEvent(new ProductSearchIndexRequestedDomainEvent(ProductId));
        }

        /// <summary>
        /// Marks this variant's ProductId as needing its Elasticsearch search document
        /// rebuilt. Public because Application-layer handlers (e.g. SetProductPriceCommand)
        /// manipulate ProductVariant directly via its DbSet rather than through the Product
        /// aggregate, and BaseEntity.AddDomainEvent is protected.
        /// </summary>
        public void RequestSearchReindex()
        {
            AddDomainEvent(new ProductSearchIndexRequestedDomainEvent(ProductId));
        }

        /// <summary>
        /// Raises ProductPriceChangedDomainEvent — previously did not exist anywhere in
        /// ProductService, so POS/Promotion's price caches (wired to product-topic
        /// earlier this session) could only ever see a product's creation-time price
        /// default of 0. Public for the same reason as RequestSearchReindex(): handlers
        /// operate on ProductVariant directly via its DbSet, not through the Product
        /// aggregate.
        /// </summary>
        public void RecordPriceChange(decimal oldPrice, decimal newPrice)
        {
            AddDomainEvent(new ProductPriceChangedDomainEvent(ProductId, Id, oldPrice, newPrice));
        }
    }
}

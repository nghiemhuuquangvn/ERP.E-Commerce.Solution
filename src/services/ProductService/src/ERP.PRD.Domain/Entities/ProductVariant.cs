using ERP.Shared.Core.Domain;

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

        public ProductVariant() { }

        public ProductVariant(string sku, string? barcode = null, decimal cost = 0)
        {
            Id = Guid.NewGuid();
            Sku = sku;
            Barcode = barcode;
            Cost = cost;
            CreatedAt = DateTime.UtcNow;
        }
    }
}

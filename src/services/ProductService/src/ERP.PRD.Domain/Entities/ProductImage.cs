using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// Product image entity for storing product images with metadata and gallery management.
    /// Supports primary image selection and custom sort order for display.
    /// </summary>
    public class ProductImage : BaseEntity
    {
        public Guid ProductId { get; set; }

        /// <summary>
        /// Optional: scopes this image to one specific variant (e.g. a color swatch
        /// photo) rather than the whole product. Null means it's a product-level image.
        /// </summary>
        public Guid? VariantId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }

        public ProductImage() { }

        public ProductImage(Guid productId, string imageUrl, string? altText = null, int sortOrder = 0, bool isPrimary = false, Guid? variantId = null)
        {
            Id = Guid.NewGuid();
            ProductId = productId;
            VariantId = variantId;
            ImageUrl = imageUrl;
            AltText = altText;
            SortOrder = sortOrder;
            IsPrimary = isPrimary;
            CreatedAt = DateTime.UtcNow;
        }

        public void SetAsPrimary()
        {
            IsPrimary = true;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateMetadata(string imageUrl, string? altText, int sortOrder)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new ArgumentException("Image URL is required", nameof(imageUrl));

            ImageUrl = imageUrl;
            AltText = altText;
            SortOrder = sortOrder;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

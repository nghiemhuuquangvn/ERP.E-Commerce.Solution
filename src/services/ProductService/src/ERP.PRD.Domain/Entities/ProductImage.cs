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
        public string ImageUrl { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }

        public ProductImage() { }

        public ProductImage(Guid productId, string imageUrl, string? altText = null, int sortOrder = 0, bool isPrimary = false)
        {
            Id = Guid.NewGuid();
            ProductId = productId;
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

        public void UpdateMetadata(string? altText, int sortOrder)
        {
            AltText = altText;
            SortOrder = sortOrder;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// A selectable option value for a SingleSelect/MultiSelect <see cref="ProductAttribute"/>.
    /// </summary>
    public class ProductAttributeOption : BaseEntity
    {
        public Guid ProductAttributeId { get; set; }
        public string Value { get; set; } = string.Empty;
        public int SortOrder { get; set; }

        public static ProductAttributeOption Create(Guid productAttributeId, string value, int sortOrder = 0) =>
            new()
            {
                Id = Guid.NewGuid(),
                ProductAttributeId = productAttributeId,
                Value = value,
                SortOrder = sortOrder,
                CreatedAt = DateTime.UtcNow
            };
    }
}

using ERP.Shared.Core.Domain;
using ERP.PRD.Domain.Enums;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// EAV-lite attribute definition (e.g. "Color", "Screen Size") that can be scoped
    /// to a Category or apply globally (CategoryId == null). Values for products/variants
    /// are stored in <see cref="ProductAttributeValue"/> rows, keyed by (OwnerType, OwnerId, ProductAttributeId).
    /// </summary>
    public class ProductAttribute : AggregateRoot
    {
        public Guid? CategoryId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public AttributeDataType DataType { get; set; }
        public string? Unit { get; set; }
        public bool IsVariantOption { get; set; }
        public bool IsRequired { get; set; }
        public bool IsFilterable { get; set; } = true;
        public int SortOrder { get; set; }

        private readonly List<ProductAttributeOption> _options = new();
        public IReadOnlyList<ProductAttributeOption> Options => _options.AsReadOnly();

        public static ProductAttribute Create(string code, string name, AttributeDataType dataType,
            Guid? categoryId = null, string? unit = null, bool isVariantOption = false,
            bool isRequired = false, bool isFilterable = true)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Attribute code is required");

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Attribute name is required");

            return new ProductAttribute
            {
                Id = Guid.NewGuid(),
                Code = code.Trim().ToLowerInvariant(),
                Name = name,
                DataType = dataType,
                CategoryId = categoryId,
                Unit = unit,
                IsVariantOption = isVariantOption,
                IsRequired = isRequired,
                IsFilterable = isFilterable,
                CreatedAt = DateTime.UtcNow
            };
        }

        public ProductAttributeOption AddOption(string value, int sortOrder = 0)
        {
            if (DataType != AttributeDataType.SingleSelect && DataType != AttributeDataType.MultiSelect)
                throw new InvalidOperationException("Options only apply to SingleSelect/MultiSelect attributes");

            if (_options.Any(o => o.Value.Equals(value, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Option '{value}' already exists for this attribute");

            var option = ProductAttributeOption.Create(Id, value, sortOrder);
            _options.Add(option);
            return option;
        }

        /// <summary>
        /// Soft-deletes this attribute. IsDeleted has a protected setter on BaseEntity,
        /// so callers outside the domain (e.g. Application-layer handlers) must go through
        /// this method rather than assigning the flag directly.
        /// </summary>
        public void Delete()
        {
            IsDeleted = true;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

using ERP.Shared.Core.Domain;
using ERP.PRD.Domain.Enums;
using ERP.PRD.Domain.Events;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// Product aggregate root representing a sellable product or service.
    /// Contains variants (SKUs), images, and category associations.
    /// </summary>
    public class Product : AggregateRoot
    {
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ProductType ProductType { get; set; }
        public ProductStatus Status { get; set; } = ProductStatus.Draft;
        public Guid? CategoryId { get; set; }
        public bool HasImage { get; set; }

        private readonly List<ProductVariant> _variants = new();
        public IReadOnlyList<ProductVariant> Variants => _variants.AsReadOnly();

        private readonly List<ProductImage> _images = new();
        public IReadOnlyList<ProductImage> Images => _images.AsReadOnly();

        private readonly List<ProductTranslation> _translations = new();
        public IReadOnlyList<ProductTranslation> Translations => _translations.AsReadOnly();

        public static Product Create(string name, string sku, ProductType productType, Guid? categoryId = null)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = name,
                Sku = sku,
                ProductType = productType,
                CategoryId = categoryId,
                Status = ProductStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };

            product.AddDomainEvent(new ProductCreatedDomainEvent(product.Id, name, sku));
            return product;
        }

        public void Publish()
        {
            if (Status != ProductStatus.Draft)
                throw new InvalidOperationException("Only Draft products can be published");

            if (!_variants.Any())
                throw new InvalidOperationException("Product must have at least one variant before publishing");

            Status = ProductStatus.Active;
            AddDomainEvent(new ProductPublishedDomainEvent(Id, Name));
        }

        public void Discontinue()
        {
            Status = ProductStatus.Discontinued;
            AddDomainEvent(new ProductDiscontinuedDomainEvent(Id, Name));
        }

        public void AddVariant(ProductVariant variant)
        {
            if (_variants.Any(v => v.Sku == variant.Sku))
                throw new InvalidOperationException($"Variant with SKU {variant.Sku} already exists");

            _variants.Add(variant);
        }

        public void AddImage(ProductImage image)
        {
            _images.Add(image);
            HasImage = true;
        }

        public void RemoveImage(Guid imageId)
        {
            var image = _images.FirstOrDefault(i => i.Id == imageId);
            if (image != null)
            {
                _images.Remove(image);
                if (!_images.Any())
                    HasImage = false;
            }
        }

        public void UpdateDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Description cannot be empty");

            Description = description.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        public void Reactivate()
        {
            if (Status != ProductStatus.Discontinued)
                throw new InvalidOperationException("Only discontinued products can be reactivated");

            Status = ProductStatus.Active;
            AddDomainEvent(new ProductPublishedDomainEvent(Id, Name));
        }

        public void RemoveVariant(Guid variantId)
        {
            var variant = _variants.FirstOrDefault(v => v.Id == variantId);
            if (variant == null)
                throw new InvalidOperationException("Variant not found");

            _variants.Remove(variant);

            if (Status == ProductStatus.Active && !_variants.Any())
                throw new InvalidOperationException("Cannot remove last variant from published product");
        }

        public bool HasVariants => _variants.Any();

        public ProductVariant? GetVariantBySku(string sku)
        {
            return _variants.FirstOrDefault(v => v.Sku == sku);
        }

        /// <summary>
        /// Adds or updates the translation for the given language code.
        /// Translations are an additive overlay; Name/Description remain the default-language fallback.
        /// </summary>
        public void SetTranslation(string languageCode, string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                throw new ArgumentException("Language code is required");

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty");

            var existing = _translations.FirstOrDefault(t => t.LanguageCode == languageCode);
            if (existing != null)
            {
                existing.Name = name;
                existing.Description = description;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _translations.Add(ProductTranslation.Create(Id, languageCode, name, description));
            }
        }
    }
}

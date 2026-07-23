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
        public new string? Description { get; set; }
        public ProductType ProductType { get; set; }
        public ProductStatus Status { get; set; } = ProductStatus.Draft;
        public Guid? CategoryId { get; set; }
        public Guid? BrandId { get; set; }

        /// <summary>
        /// References SharedMasterDataService's UnitOfMeasure by Id only (cross-service,
        /// no database FK — see CLAUDE.md database ownership rules).
        /// </summary>
        public Guid? BaseUomId { get; set; }
        public bool HasImage { get; set; }

        // SEO fields for storefront (product detail page <title>/<meta>/canonical URL)
        public string? Slug { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }

        // Denormalized read-model projection synced from WarehouseService's
        // InventoryUpdatedEvent (RabbitMQ, warehouse_exchange) — SQL there remains the
        // system-of-record; this is only for fast "in stock?" display/filtering here
        // without a synchronous cross-service call on every catalog read.
        public int StockQuantity { get; set; }
        public DateTime? LastStockSyncAt { get; set; }

        // Opaque escape hatch for arbitrary structured data (never queried via SQL JSON functions)
        public string? ExtendedDataJson { get; set; }

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

        /// <summary>
        /// Moves Draft → PendingReview. Optional moderation step: Publish() still works
        /// directly from Draft for teams that don't want a review gate, so this doesn't
        /// change the pre-existing one-step flow — it just adds a second, explicit
        /// two-step Draft → PendingReview → Active path for teams that do.
        /// </summary>
        public void SubmitForReview()
        {
            if (Status != ProductStatus.Draft)
                throw new InvalidOperationException("Only Draft products can be submitted for review");

            Status = ProductStatus.PendingReview;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Publish()
        {
            if (Status != ProductStatus.Draft && Status != ProductStatus.PendingReview)
                throw new InvalidOperationException("Only Draft or PendingReview products can be published");

            if (!_variants.Any())
                throw new InvalidOperationException("Product must have at least one variant before publishing");

            Status = ProductStatus.Active;
            AddDomainEvent(new ProductPublishedDomainEvent(Id, Name));
            AddDomainEvent(new ProductSearchIndexRequestedDomainEvent(Id));
        }

        public void Discontinue()
        {
            Status = ProductStatus.Discontinued;
            AddDomainEvent(new ProductDiscontinuedDomainEvent(Id, Name));
            AddDomainEvent(new ProductSearchIndexRequestedDomainEvent(Id));
        }

        public void AddVariant(ProductVariant variant)
        {
            if (_variants.Any(v => v.Sku == variant.Sku))
                throw new InvalidOperationException($"Variant with SKU {variant.Sku} already exists");

            _variants.Add(variant);
            AddDomainEvent(new ProductSearchIndexRequestedDomainEvent(Id));
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
            AddDomainEvent(new ProductSearchIndexRequestedDomainEvent(Id));
            AddDomainEvent(new ProductUpdatedDomainEvent(Id, Name, Sku));
        }

        public void SetBrand(Guid? brandId)
        {
            BrandId = brandId;
            UpdatedAt = DateTime.UtcNow;
            AddDomainEvent(new ProductSearchIndexRequestedDomainEvent(Id));
        }

        public void SetBaseUom(Guid? baseUomId)
        {
            BaseUomId = baseUomId;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetSeoFields(string? slug, string? metaTitle, string? metaDescription)
        {
            Slug = string.IsNullOrWhiteSpace(slug) ? null : slug.Trim().ToLowerInvariant();
            MetaTitle = metaTitle?.Trim();
            MetaDescription = metaDescription?.Trim();
            UpdatedAt = DateTime.UtcNow;
            AddDomainEvent(new ProductSearchIndexRequestedDomainEvent(Id));
        }

        /// <summary>
        /// Applies a stock-level projection update received from WarehouseService's
        /// InventoryUpdatedEvent. Deliberately does not raise ProductUpdatedDomainEvent —
        /// that event is for other services to resync, and Warehouse is the one that just
        /// told us this, so re-publishing it back out would be a pointless echo.
        /// </summary>
        public void SyncStockQuantity(int quantity)
        {
            StockQuantity = quantity;
            LastStockSyncAt = DateTime.UtcNow;
            AddDomainEvent(new ProductSearchIndexRequestedDomainEvent(Id));
        }

        public void SetExtendedData(string? json)
        {
            ExtendedDataJson = json;
            UpdatedAt = DateTime.UtcNow;
            AddDomainEvent(new ProductSearchIndexRequestedDomainEvent(Id));
        }

        public void Reactivate()
        {
            if (Status != ProductStatus.Discontinued)
                throw new InvalidOperationException("Only discontinued products can be reactivated");

            Status = ProductStatus.Active;
            AddDomainEvent(new ProductPublishedDomainEvent(Id, Name));
            AddDomainEvent(new ProductSearchIndexRequestedDomainEvent(Id));
        }

        public void RemoveVariant(Guid variantId)
        {
            var variant = _variants.FirstOrDefault(v => v.Id == variantId);
            if (variant == null)
                throw new InvalidOperationException("Variant not found");

            _variants.Remove(variant);

            if (Status == ProductStatus.Active && !_variants.Any())
                throw new InvalidOperationException("Cannot remove last variant from published product");

            AddDomainEvent(new ProductSearchIndexRequestedDomainEvent(Id));
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

            AddDomainEvent(new ProductSearchIndexRequestedDomainEvent(Id));
        }
    }
}

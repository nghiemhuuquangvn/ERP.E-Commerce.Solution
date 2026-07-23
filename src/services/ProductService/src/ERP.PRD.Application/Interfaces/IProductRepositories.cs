using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;

namespace ERP.PRD.Application.Interfaces
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Product?> GetByIdWithVariantsAsync(Guid id, CancellationToken ct = default);
        Task<Product?> GetByIdWithTranslationsAsync(Guid id, CancellationToken ct = default);
        Task<Product?> GetByIdWithVariantsAndTranslationsAsync(Guid id, CancellationToken ct = default);
        Task<Product?> GetByIdWithImagesAsync(Guid id, CancellationToken ct = default);
        Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default);
        Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
        Task<bool> SkuExistsAsync(string sku, CancellationToken ct = default);
        Task<bool> SlugTakenByAnotherAsync(Guid productId, string slug, CancellationToken ct = default);
        Task<(List<Product> Items, int Total)> GetPagedAsync(
            int skip, int take,
            Guid? categoryId, int? status,
            CancellationToken ct = default);
        Task<List<Product>> SearchAsync(string searchTerm, CancellationToken ct = default);
        Task<List<Guid>> GetAllActiveIdsAsync(CancellationToken ct = default);
        Task<List<string>> GetExportRowsAsync(Guid? categoryId, CancellationToken ct = default);
        Task AddAsync(Product product, CancellationToken ct = default);
        void Update(Product product);
    }

    public interface ICategoryRepository
    {
        Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Category?> GetByIdWithTranslationsAsync(Guid id, CancellationToken ct = default);
        Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
        Task<List<Category>> GetByParentIdAsync(Guid? parentId, CancellationToken ct = default);
        Task AddAsync(Category category, CancellationToken ct = default);
        void Update(Category category);
    }

    public interface IProductVariantRepository
    {
        Task<ProductVariant?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<ProductVariant?> GetBySkuAsync(string sku, CancellationToken ct = default);
        Task<List<ProductVariant>> GetByProductIdAsync(Guid productId, CancellationToken ct = default);
        Task<List<VariantPriceProjection>> GetPricesByProductIdAsync(Guid productId, CancellationToken ct = default);
        void Update(ProductVariant variant);
    }

    public interface IProductAttributeRepository
    {
        Task<ProductAttribute?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<ProductAttribute?> GetByIdWithOptionsAsync(Guid id, CancellationToken ct = default);
        Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
        Task<List<ProductAttribute>> GetByCategoryAsync(Guid? categoryId, CancellationToken ct = default);
        Task<Dictionary<Guid, ProductAttribute>> GetByIdsWithOptionsAsDictionaryAsync(
            IEnumerable<Guid> ids, CancellationToken ct = default);
        Task AddAsync(ProductAttribute attribute, CancellationToken ct = default);
        void Update(ProductAttribute attribute);
    }

    public interface IProductAttributeValueRepository
    {
        Task<ProductAttributeValue?> GetExistingAsync(
            AttributeOwnerType ownerType, Guid ownerId, Guid attributeId, CancellationToken ct = default);
        Task<List<ProductAttributeValue>> GetByOwnerAsync(
            Guid productId, Guid? variantId, CancellationToken ct = default);
        Task<bool> AnyByAttributeIdAsync(Guid attributeId, CancellationToken ct = default);
        Task<Guid> GetProductIdForVariantAsync(Guid variantId, CancellationToken ct = default);
        Task AddAsync(ProductAttributeValue value, CancellationToken ct = default);
        void Update(ProductAttributeValue value);
    }

    public interface IProductImageRepository
    {
        Task<List<ProductImageProjection>> GetByProductIdAsync(Guid productId, CancellationToken ct = default);
    }

    public interface IPriceListRepository
    {
        Task<PriceList?> GetByIdWithItemsAsync(Guid id, CancellationToken ct = default);
        Task<ProductVariant?> GetVariantByIdAsync(Guid variantId, CancellationToken ct = default);
        Task AddAsync(PriceList priceList, CancellationToken ct = default);
    }

    public interface IBrandRepository
    {
        Task<Brand?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
        Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
        Task<List<Brand>> GetAllAsync(bool? isActive, CancellationToken ct = default);
        Task AddAsync(Brand brand, CancellationToken ct = default);
        void Update(Brand brand);
    }

    public interface IProductUomConversionRepository
    {
        Task<ProductUomConversion?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<ProductUomConversion>> GetByProductIdAsync(Guid productId, CancellationToken ct = default);
        Task<bool> PairExistsAsync(Guid productId, Guid fromUomId, Guid toUomId, CancellationToken ct = default);
        Task AddAsync(ProductUomConversion conversion, CancellationToken ct = default);
        void Update(ProductUomConversion conversion);
    }

    public interface IProductBundleRepository
    {
        Task<bool> ProductExistsAsync(Guid productId, CancellationToken ct = default);
        Task<object?> FindByIdAsync(Guid bundleItemId, CancellationToken ct = default);
        Task<List<BundleItemProjection>> GetByBundleProductIdAsync(Guid bundleProductId, CancellationToken ct = default);
        Task AddAsync(ProductBundleItem item, CancellationToken ct = default);
        void Remove(object item);
    }

    public interface IProductRelationRepository
    {
        Task<object?> FindByIdAsync(Guid relationId, CancellationToken ct = default);
        Task<List<ProductRelationProjection>> GetByProductIdAsync(
            Guid productId, ProductRelationType? relationType, CancellationToken ct = default);
        Task AddAsync(ProductRelation relation, CancellationToken ct = default);
        void Remove(object relation);
    }

    public interface IProductReviewRepository
    {
        Task<bool> ProductExistsAsync(Guid productId, CancellationToken ct = default);
        Task<(List<ProductReviewProjection> Items, int Total, double Average)> GetPagedByProductIdAsync(
            Guid productId, int skip, int take, CancellationToken ct = default);
        Task AddAsync(ProductReview review, CancellationToken ct = default);
    }

    public interface ISearchIndexQueueRepository
    {
        Task<List<Guid>> GetAllActiveProductIdsAsync(CancellationToken ct = default);
        Task UpsertBatchAsync(IEnumerable<Guid> productIds, CancellationToken ct = default);
    }

    // --- Projection types (flat read-model structs, no Infrastructure references) ---

    public record VariantPriceProjection(Guid VariantId, string Sku, decimal Cost, DateTime UpdatedAt);

    public record ProductImageProjection(
        Guid Id, string ImageUrl, string? AltText, int SortOrder, bool IsPrimary, DateTime CreatedAt,
        Guid? VariantId = null);

    public record BundleItemProjection(
        Guid Id, Guid ComponentProductId, string ComponentName, string ComponentSku, int Quantity);

    public record ProductRelationProjection(
        Guid Id, Guid RelatedProductId, string RelatedName, string RelatedSku,
        ERP.PRD.Domain.Enums.ProductRelationType RelationType, int SortOrder);

    public record ProductReviewProjection(
        Guid Id, Guid CustomerId, int Rating, string? Comment, DateTime CreatedAt);
}

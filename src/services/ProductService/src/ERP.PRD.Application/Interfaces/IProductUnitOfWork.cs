namespace ERP.PRD.Application.Interfaces
{
    public interface IProductUnitOfWork : IDisposable
    {
        IProductRepository Products { get; }
        ICategoryRepository Categories { get; }
        IProductVariantRepository Variants { get; }
        IProductAttributeRepository Attributes { get; }
        IProductAttributeValueRepository AttributeValues { get; }
        IProductImageRepository Images { get; }
        IPriceListRepository PriceLists { get; }
        IProductBundleRepository Bundles { get; }
        IProductRelationRepository Relations { get; }
        IProductReviewRepository Reviews { get; }
        ISearchIndexQueueRepository SearchIndexQueue { get; }
        IBrandRepository Brands { get; }
        IProductUomConversionRepository UomConversions { get; }

        Task<int> SaveChangesAsync(CancellationToken ct = default);
        Task BeginTransactionAsync(CancellationToken ct = default);
        Task CommitTransactionAsync(CancellationToken ct = default);
        Task RollbackTransactionAsync(CancellationToken ct = default);
    }
}

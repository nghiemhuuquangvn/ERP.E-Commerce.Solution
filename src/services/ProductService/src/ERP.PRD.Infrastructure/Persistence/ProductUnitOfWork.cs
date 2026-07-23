using ERP.PRD.Application.Interfaces;
using ERP.PRD.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.PRD.Infrastructure.Persistence
{
    public class ProductUnitOfWork : IProductUnitOfWork
    {
        private readonly ProductDbContext _db;
        private IDbContextTransaction? _currentTrans;

        public ProductUnitOfWork(
            ProductDbContext db,
            IProductRepository products,
            ICategoryRepository categories,
            IProductVariantRepository variants,
            IProductAttributeRepository attributes,
            IProductAttributeValueRepository attributeValues,
            IProductImageRepository images,
            IPriceListRepository priceLists,
            IProductBundleRepository bundles,
            IProductRelationRepository relations,
            IProductReviewRepository reviews,
            ISearchIndexQueueRepository searchIndexQueue,
            IBrandRepository brands,
            IProductUomConversionRepository uomConversions)
        {
            _db = db;
            Products = products;
            Categories = categories;
            Variants = variants;
            Attributes = attributes;
            AttributeValues = attributeValues;
            Images = images;
            PriceLists = priceLists;
            Bundles = bundles;
            Relations = relations;
            Reviews = reviews;
            SearchIndexQueue = searchIndexQueue;
            Brands = brands;
            UomConversions = uomConversions;
        }

        public IProductRepository Products { get; }
        public ICategoryRepository Categories { get; }
        public IProductVariantRepository Variants { get; }
        public IProductAttributeRepository Attributes { get; }
        public IProductAttributeValueRepository AttributeValues { get; }
        public IProductImageRepository Images { get; }
        public IPriceListRepository PriceLists { get; }
        public IProductBundleRepository Bundles { get; }
        public IProductRelationRepository Relations { get; }
        public IProductReviewRepository Reviews { get; }
        public ISearchIndexQueueRepository SearchIndexQueue { get; }
        public IBrandRepository Brands { get; }
        public IProductUomConversionRepository UomConversions { get; }

        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
            => await _db.SaveChangesAsync(ct);

        public async Task BeginTransactionAsync(CancellationToken ct = default)
        {
            _currentTrans = await _db.Database.BeginTransactionAsync(ct);
        }

        public async Task CommitTransactionAsync(CancellationToken ct = default)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
                if (_currentTrans != null)
                    await _currentTrans.CommitAsync(ct);
            }
            catch
            {
                await RollbackTransactionAsync(ct);
                throw;
            }
            finally
            {
                if (_currentTrans != null)
                {
                    _currentTrans.Dispose();
                    _currentTrans = null;
                }
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken ct = default)
        {
            if (_currentTrans != null)
            {
                await _currentTrans.RollbackAsync(ct);
                _currentTrans.Dispose();
                _currentTrans = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
                _currentTrans?.Dispose();
            }
        }
    }
}

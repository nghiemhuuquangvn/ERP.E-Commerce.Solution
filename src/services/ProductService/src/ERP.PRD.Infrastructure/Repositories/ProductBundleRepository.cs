using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Infrastructure.Repositories
{
    internal sealed class ProductBundleRepository : IProductBundleRepository
    {
        private readonly ProductDbContext _db;

        public ProductBundleRepository(ProductDbContext db) => _db = db;

        public async Task<bool> ProductExistsAsync(Guid productId, CancellationToken ct = default)
            => await _db.Products.AnyAsync(p => p.Id == productId && !p.IsDeleted, ct);

        public async Task<object?> FindByIdAsync(Guid bundleItemId, CancellationToken ct = default)
            => await _db.ProductBundleItems.FindAsync(new object[] { bundleItemId }, ct);

        public async Task<List<BundleItemProjection>> GetByBundleProductIdAsync(
            Guid bundleProductId, CancellationToken ct = default)
            => await (
                from item in _db.ProductBundleItems
                join component in _db.Products on item.ComponentProductId equals component.Id
                where item.BundleProductId == bundleProductId
                select new BundleItemProjection(
                    item.Id,
                    component.Id,
                    component.Name,
                    component.Sku,
                    item.Quantity)
            ).ToListAsync(ct);

        public async Task AddAsync(ProductBundleItem item, CancellationToken ct = default)
            => await _db.ProductBundleItems.AddAsync(item, ct);

        public void Remove(object item)
        {
            if (item is ProductBundleItem bundleItem)
                _db.ProductBundleItems.Remove(bundleItem);
        }
    }
}

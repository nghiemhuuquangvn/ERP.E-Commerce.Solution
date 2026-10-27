using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Infrastructure.Repositories
{
    internal sealed class ProductVariantRepository : IProductVariantRepository
    {
        private readonly ProductDbContext _db;

        public ProductVariantRepository(ProductDbContext db) => _db = db;

        public async Task<ProductVariant?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, ct);

        public async Task<ProductVariant?> GetBySkuAsync(string sku, CancellationToken ct = default)
        {
            // Search all non-deleted variants across products
            var normalizedSku = sku.ToUpper().Trim();
            return await _db.ProductVariants
                .FirstOrDefaultAsync(v => v.Sku == normalizedSku, ct);
        }

        public async Task<List<ProductVariant>> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
            => await _db.ProductVariants
                .Where(v => v.ProductId == productId && !v.IsDeleted)
                .OrderBy(v => v.Sku)
                .ToListAsync(ct);

        public async Task<List<ProductVariant>> GetByProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
            => await _db.ProductVariants
                .Where(v => productIds.Contains(v.ProductId) && !v.IsDeleted)
                .OrderBy(v => v.Sku)
                .ToListAsync(ct);

        public async Task<List<VariantPriceProjection>> GetPricesByProductIdAsync(
            Guid productId, CancellationToken ct = default)
            => await _db.ProductVariants
                .Where(v => v.ProductId == productId)
                .OrderBy(v => v.Sku)
                .Select(v => new VariantPriceProjection(v.Id, v.Sku, v.Cost, v.UpdatedAt))
                .ToListAsync(ct);

        public void Update(ProductVariant variant)
            => _db.ProductVariants.Update(variant);
    }
}

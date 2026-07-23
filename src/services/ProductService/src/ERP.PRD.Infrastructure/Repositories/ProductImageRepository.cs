using ERP.PRD.Application.Interfaces;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Infrastructure.Repositories
{
    internal sealed class ProductImageRepository : IProductImageRepository
    {
        private readonly ProductDbContext _db;

        public ProductImageRepository(ProductDbContext db) => _db = db;

        public async Task<List<ProductImageProjection>> GetByProductIdAsync(
            Guid productId, CancellationToken ct = default)
            => await _db.ProductImages
                .Where(i => i.ProductId == productId)
                .OrderBy(i => i.IsPrimary ? 0 : 1)
                .ThenBy(i => i.SortOrder)
                .Select(i => new ProductImageProjection(
                    i.Id, i.ImageUrl, i.AltText, i.SortOrder, i.IsPrimary, i.CreatedAt, i.VariantId))
                .ToListAsync(ct);
    }
}

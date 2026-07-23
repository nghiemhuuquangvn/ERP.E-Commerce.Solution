using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Infrastructure.Repositories
{
    internal sealed class ProductUomConversionRepository : IProductUomConversionRepository
    {
        private readonly ProductDbContext _db;

        public ProductUomConversionRepository(ProductDbContext db) => _db = db;

        public async Task<ProductUomConversion?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await _db.ProductUomConversions.FirstOrDefaultAsync(c => c.Id == id, ct);

        public async Task<List<ProductUomConversion>> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
            => await _db.ProductUomConversions
                .Where(c => c.ProductId == productId)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(ct);

        public async Task<bool> PairExistsAsync(Guid productId, Guid fromUomId, Guid toUomId, CancellationToken ct = default)
            => await _db.ProductUomConversions.AnyAsync(
                c => c.ProductId == productId && c.FromUomId == fromUomId && c.ToUomId == toUomId, ct);

        public async Task AddAsync(ProductUomConversion conversion, CancellationToken ct = default)
            => await _db.ProductUomConversions.AddAsync(conversion, ct);

        public void Update(ProductUomConversion conversion)
            => _db.ProductUomConversions.Update(conversion);
    }
}

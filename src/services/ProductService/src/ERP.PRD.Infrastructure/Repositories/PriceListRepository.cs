using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Infrastructure.Repositories
{
    internal sealed class PriceListRepository : IPriceListRepository
    {
        private readonly ProductDbContext _db;

        public PriceListRepository(ProductDbContext db) => _db = db;

        public async Task<PriceList?> GetByIdWithItemsAsync(Guid id, CancellationToken ct = default)
            => await _db.PriceLists
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

        public async Task<ProductVariant?> GetVariantByIdAsync(Guid variantId, CancellationToken ct = default)
            => await _db.ProductVariants.FindAsync(new object[] { variantId }, ct);

        public async Task AddAsync(PriceList priceList, CancellationToken ct = default)
            => await _db.PriceLists.AddAsync(priceList, ct);
    }
}

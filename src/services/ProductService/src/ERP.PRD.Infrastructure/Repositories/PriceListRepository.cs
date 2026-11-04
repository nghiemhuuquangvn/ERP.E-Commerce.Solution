using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
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

        public async Task<PriceList?> GetDefaultRetailListAsync(CancellationToken ct = default)
            => await _db.PriceLists
                .Include(p => p.Items)
                .Where(p => p.Name == PriceList.DefaultRetailName && p.Channel == PriceListChannel.Retail && p.IsActive)
                .OrderBy(p => p.CreatedAt)
                .FirstOrDefaultAsync(ct);

        public void MarkItemAdded(PriceListItem item)
            => _db.Entry(item).State = EntityState.Added;

        public async Task<List<Guid>> GetProductIdsOfVariantsAsync(IEnumerable<Guid> variantIds, CancellationToken ct = default)
        {
            var ids = variantIds.Distinct().ToList();
            return await _db.ProductVariants
                .Where(v => ids.Contains(v.Id) && !v.IsDeleted)
                .Select(v => v.ProductId)
                .Distinct()
                .ToListAsync(ct);
        }
    }
}

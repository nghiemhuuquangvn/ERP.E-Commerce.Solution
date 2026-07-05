using ERP.PRD.Application.Interfaces;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Infrastructure.Repositories
{
    internal sealed class SearchIndexQueueRepository : ISearchIndexQueueRepository
    {
        private readonly ProductDbContext _db;

        public SearchIndexQueueRepository(ProductDbContext db) => _db = db;

        public async Task<List<Guid>> GetAllActiveProductIdsAsync(CancellationToken ct = default)
            => await _db.Products
                .Where(p => !p.IsDeleted)
                .Select(p => p.Id)
                .ToListAsync(ct);

        public async Task UpsertBatchAsync(IEnumerable<Guid> productIds, CancellationToken ct = default)
        {
            var idList = productIds.ToList();
            if (idList.Count == 0) return;

            var existingPending = await _db.SearchIndexQueue
                .Where(q => q.IndexedAt == null && idList.Contains(q.ProductId))
                .ToListAsync(ct);

            var existingByProductId = existingPending
                .GroupBy(q => q.ProductId)
                .ToDictionary(g => g.Key, g => g.First());

            var now = DateTime.UtcNow;

            foreach (var productId in idList)
            {
                if (existingByProductId.TryGetValue(productId, out var pending))
                {
                    pending.RequestedAt = now;
                }
                else
                {
                    await _db.SearchIndexQueue.AddAsync(new SearchIndexQueueEntry
                    {
                        ProductId = productId,
                        RequestedAt = now
                    }, ct);
                }
            }
        }
    }
}

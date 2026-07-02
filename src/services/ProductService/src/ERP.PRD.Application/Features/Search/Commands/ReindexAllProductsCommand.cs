using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Search.Commands
{
    /// <summary>
    /// Administrative bulk action: enqueue every non-deleted Product for an Elasticsearch
    /// reindex. Bypasses the domain-event path (this isn't itself a state change) and inserts/
    /// bumps SearchIndexQueue rows directly, mirroring ProductDbContext.SaveChangesAsync's own
    /// upsert-by-ProductId dedup logic.
    /// </summary>
    public class ReindexAllProductsCommand : IRequest<int>
    {
    }

    public class ReindexAllProductsCommandHandler : IRequestHandler<ReindexAllProductsCommand, int>
    {
        private readonly ProductDbContext _context;

        public ReindexAllProductsCommandHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(ReindexAllProductsCommand request, CancellationToken cancellationToken)
        {
            var productIds = await _context.Products
                .Where(p => !p.IsDeleted)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            if (productIds.Count == 0)
                return 0;

            var existingPending = await _context.SearchIndexQueue
                .Where(q => q.IndexedAt == null && productIds.Contains(q.ProductId))
                .ToListAsync(cancellationToken);

            var existingByProductId = existingPending
                .GroupBy(q => q.ProductId)
                .ToDictionary(g => g.Key, g => g.First());

            var now = DateTime.UtcNow;

            foreach (var productId in productIds)
            {
                if (existingByProductId.TryGetValue(productId, out var pendingEntry))
                {
                    pendingEntry.RequestedAt = now;
                }
                else
                {
                    await _context.SearchIndexQueue.AddAsync(new SearchIndexQueueEntry
                    {
                        ProductId = productId,
                        RequestedAt = now
                    }, cancellationToken);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return productIds.Count;
        }
    }
}

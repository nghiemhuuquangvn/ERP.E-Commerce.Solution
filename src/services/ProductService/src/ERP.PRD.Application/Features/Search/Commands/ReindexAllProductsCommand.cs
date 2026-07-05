using ERP.PRD.Application.Interfaces;
using MediatR;

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
        private readonly IProductUnitOfWork _uow;

        public ReindexAllProductsCommandHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<int> Handle(ReindexAllProductsCommand request, CancellationToken cancellationToken)
        {
            var productIds = await _uow.SearchIndexQueue.GetAllActiveProductIdsAsync(cancellationToken);

            if (productIds.Count == 0)
                return 0;

            await _uow.SearchIndexQueue.UpsertBatchAsync(productIds, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            return productIds.Count;
        }
    }
}

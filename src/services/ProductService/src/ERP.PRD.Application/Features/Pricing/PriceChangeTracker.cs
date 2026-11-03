using ERP.PRD.Application.Interfaces;

namespace ERP.PRD.Application.Features.Pricing
{
    /// <summary>What the products sold for before a price-list change.</summary>
    public sealed record PriceSnapshot(IReadOnlyList<Guid> ProductIds, Dictionary<Guid, ResolvedProductPrice> Prices);

    /// <summary>
    /// Turns a price-list change into ProductPriceChanged events: take a snapshot of the affected products' retail prices, save the change, then
    /// <see cref="PublishChangesAsync"/> compares and raises the event on each product whose price moved (POS refreshes its price cache from it).
    /// Callers wrap both saves in one transaction so a price is never stored without its event.
    /// </summary>
    public sealed class PriceChangeTracker
    {
        private readonly IProductUnitOfWork _uow;
        private readonly IProductPriceResolver _prices;

        public PriceChangeTracker(IProductUnitOfWork uow, IProductPriceResolver prices)
        {
            _uow = uow;
            _prices = prices;
        }

        public async Task<PriceSnapshot> SnapshotAsync(IEnumerable<Guid> productIds, CancellationToken ct = default)
        {
            var ids = productIds.Distinct().ToList();
            return new PriceSnapshot(ids, await _prices.GetProductPricesAsync(ids, ct));
        }

        /// <summary>Call AFTER the price-list change was saved. Returns how many products got an event.</summary>
        public async Task<int> PublishChangesAsync(PriceSnapshot before, CancellationToken ct = default)
        {
            var after = await _prices.GetProductPricesAsync(before.ProductIds, ct);
            var raised = 0;
            foreach (var productId in before.ProductIds)
            {
                var product = await _uow.Products.GetByIdAsync(productId, ct);
                if (product != null && ProductPriceEvents.RaiseIfChanged(product, before.Prices, after))
                    raised++;
            }

            if (raised > 0)
                await _uow.SaveChangesAsync(ct);
            return raised;
        }
    }
}

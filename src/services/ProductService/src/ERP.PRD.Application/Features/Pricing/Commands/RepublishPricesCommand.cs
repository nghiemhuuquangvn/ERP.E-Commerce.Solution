using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Pricing.Commands
{
    /// <summary>
    /// Re-sends ProductPriceChanged (old = new = today's retail price) for every priced product of the shop, so a POS price cache that never saw a price
    /// (products priced before the price events carried real prices, or a price that changed only by date) catches up. Safe to run again: the consumers
    /// just set the cache to the same figure, and Sales ignores an event whose old and new price are equal.
    /// </summary>
    public class RepublishPricesCommand : IRequest<RepublishPricesResult>
    {
    }

    public class RepublishPricesResult
    {
        public int ProductsChecked { get; set; }
        public int EventsRaised { get; set; }
    }

    public class RepublishPricesHandler : IRequestHandler<RepublishPricesCommand, RepublishPricesResult>
    {
        private const int PageSize = 200;

        private readonly IProductUnitOfWork _uow;
        private readonly IProductPriceResolver _prices;
        private readonly ILogger<RepublishPricesHandler> _logger;

        public RepublishPricesHandler(IProductUnitOfWork uow, IProductPriceResolver prices, ILogger<RepublishPricesHandler> logger)
        {
            _uow = uow;
            _prices = prices;
            _logger = logger;
        }

        public async Task<RepublishPricesResult> Handle(RepublishPricesCommand request, CancellationToken cancellationToken)
        {
            var ids = await _uow.Products.GetAllActiveIdsAsync(cancellationToken);
            var result = new RepublishPricesResult { ProductsChecked = ids.Count };

            foreach (var page in ids.Chunk(PageSize))
            {
                var prices = await _prices.GetProductPricesAsync(page, cancellationToken);
                foreach (var productId in page)
                {
                    if (!prices.TryGetValue(productId, out var price))
                        continue;

                    var product = await _uow.Products.GetByIdAsync(productId, cancellationToken);
                    if (product == null)
                        continue;

                    product.RecordPriceChange(price.VariantId, price.Price, price.Price);
                    result.EventsRaised++;
                }

                await _uow.SaveChangesAsync(cancellationToken);   // writes the page's events to the outbox and clears them
            }

            _logger.LogInformation("Republished retail prices: {Checked} products checked, {Raised} events raised", result.ProductsChecked, result.EventsRaised);
            return result;
        }
    }
}

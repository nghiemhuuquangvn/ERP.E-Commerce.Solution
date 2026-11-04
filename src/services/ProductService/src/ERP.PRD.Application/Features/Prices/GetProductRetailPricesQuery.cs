using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Prices
{
    /// <summary>The retail price of each product (R111's rule: the cheapest priced variant); products without a price are left out. Asked by SaleOrderService so an order is priced by the server (R113).</summary>
    public class GetProductRetailPricesQuery : IRequest<List<ProductRetailPriceDto>>
    {
        public List<Guid> ProductIds { get; set; } = new();
    }

    public class ProductRetailPriceDto
    {
        public Guid ProductId { get; set; }
        public decimal Price { get; set; }
    }

    public class GetProductRetailPricesHandler : IRequestHandler<GetProductRetailPricesQuery, List<ProductRetailPriceDto>>
    {
        private readonly IProductPriceResolver _prices;

        public GetProductRetailPricesHandler(IProductPriceResolver prices) => _prices = prices;

        public async Task<List<ProductRetailPriceDto>> Handle(GetProductRetailPricesQuery request, CancellationToken cancellationToken)
        {
            var ids = request.ProductIds.Distinct().ToList();
            if (ids.Count == 0) return new();

            var resolved = await _prices.GetProductPricesAsync(ids, cancellationToken);
            return resolved.Select(p => new ProductRetailPriceDto { ProductId = p.Key, Price = p.Value.Price }).ToList();
        }
    }
}

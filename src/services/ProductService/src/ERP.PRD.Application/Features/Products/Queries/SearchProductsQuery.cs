using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Products.Queries
{
    public class SearchProductsQuery : IRequest<List<ProductDto>>
    {
        public string SearchTerm { get; set; } = string.Empty;
        public string? LanguageCode { get; set; }
    }

    public class SearchProductsHandler : IRequestHandler<SearchProductsQuery, List<ProductDto>>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly IProductPriceResolver _prices;

        public SearchProductsHandler(IProductUnitOfWork uow, IProductPriceResolver prices)
        {
            _uow = uow;
            _prices = prices;
        }

        public async Task<List<ProductDto>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.SearchTerm))
                return new();

            var products = await _uow.Products.SearchAsync(request.SearchTerm, cancellationToken);

            var prices = await _prices.GetProductPricesAsync(products.Select(p => p.Id).ToList(), cancellationToken);

            return products
                .OrderByDescending(p => p.Name.StartsWith(request.SearchTerm, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(p => p.CreatedAt)
                .Take(50)
                .Select(p => ProductTranslationMapper.MapToDto(p, request.LanguageCode, prices.TryGetValue(p.Id, out var rp) ? rp.Price : 0m))
                .ToList();
        }
    }
}

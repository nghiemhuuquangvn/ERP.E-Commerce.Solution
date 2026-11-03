using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Products.Queries
{
    public class ListProductsQuery : IRequest<ListProductsResponse>
    {
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 20;
        public Guid? CategoryId { get; set; }
        public int? Status { get; set; }
        public string? LanguageCode { get; set; }
    }

    public class ListProductsResponse
    {
        public int Total { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
        public List<ProductDto> Products { get; set; } = new();
    }

    public class ListProductsHandler : IRequestHandler<ListProductsQuery, ListProductsResponse>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly IProductPriceResolver _prices;

        public ListProductsHandler(IProductUnitOfWork uow, IProductPriceResolver prices)
        {
            _uow = uow;
            _prices = prices;
        }

        public async Task<ListProductsResponse> Handle(ListProductsQuery request, CancellationToken cancellationToken)
        {
            var (items, total) = await _uow.Products.GetPagedAsync(
                request.Skip, request.Take,
                request.CategoryId, request.Status,
                cancellationToken);

            var prices = await _prices.GetProductPricesAsync(items.Select(p => p.Id).ToList(), cancellationToken);

            return new ListProductsResponse
            {
                Total = total,
                Skip = request.Skip,
                Take = request.Take,
                Products = items.Select(p => ProductTranslationMapper.MapToDto(p, request.LanguageCode, prices.TryGetValue(p.Id, out var rp) ? rp.Price : 0m)).ToList()
            };
        }
    }
}

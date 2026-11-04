using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Products.Queries
{
    public class GetProductBySkuQuery : IRequest<ProductDto>
    {
        public string Sku { get; set; } = string.Empty;
        public string? LanguageCode { get; set; }
    }

    public class GetProductBySkuHandler : IRequestHandler<GetProductBySkuQuery, ProductDto>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly IProductPriceResolver _prices;

        public GetProductBySkuHandler(IProductUnitOfWork uow, IProductPriceResolver prices)
        {
            _uow = uow;
            _prices = prices;
        }

        public async Task<ProductDto> Handle(GetProductBySkuQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Sku))
                throw new ArgumentException("SKU is required");

            var product = await _uow.Products.GetBySkuAsync(request.Sku, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with SKU {request.Sku} not found");

            var prices = await _prices.GetProductPricesAsync(new[] { product.Id }, cancellationToken);
            return ProductTranslationMapper.MapToDto(product, request.LanguageCode, prices.TryGetValue(product.Id, out var p) ? p.Price : 0m);
        }
    }
}

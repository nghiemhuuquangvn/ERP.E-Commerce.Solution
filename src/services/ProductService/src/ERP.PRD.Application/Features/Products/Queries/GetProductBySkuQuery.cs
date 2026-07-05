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

        public GetProductBySkuHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ProductDto> Handle(GetProductBySkuQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Sku))
                throw new ArgumentException("SKU is required");

            var product = await _uow.Products.GetBySkuAsync(request.Sku, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with SKU {request.Sku} not found");

            return ProductTranslationMapper.MapToDto(product, request.LanguageCode);
        }
    }
}

using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Products.Queries
{
    public class GetProductQuery : IRequest<ProductDto>
    {
        public Guid ProductId { get; set; }
        public string? LanguageCode { get; set; }
    }

    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ProductType { get; set; }
        public int Status { get; set; }
        public Guid? CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public Guid? BrandId { get; set; }
        public Guid? BaseUomId { get; set; }
        public bool HasImage { get; set; }
        public string? ImageUrl { get; set; }
        public int VariantCount { get; set; }
        public int StockQuantity { get; set; }
        public decimal Price { get; set; }
        public string LanguageCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class GetProductHandler : IRequestHandler<GetProductQuery, ProductDto>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly IProductPriceResolver _prices;

        public GetProductHandler(IProductUnitOfWork uow, IProductPriceResolver prices)
        {
            _uow = uow;
            _prices = prices;
        }

        public async Task<ProductDto> Handle(GetProductQuery request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdWithVariantsAndTranslationsAsync(
                request.ProductId, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var prices = await _prices.GetProductPricesAsync(new[] { product.Id }, cancellationToken);
            return ProductTranslationMapper.MapToDto(product, request.LanguageCode, prices.TryGetValue(product.Id, out var p) ? p.Price : 0m);
        }
    }
}

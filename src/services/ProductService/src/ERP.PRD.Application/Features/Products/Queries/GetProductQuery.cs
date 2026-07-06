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

        public GetProductHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ProductDto> Handle(GetProductQuery request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdWithVariantsAndTranslationsAsync(
                request.ProductId, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            return ProductTranslationMapper.MapToDto(product, request.LanguageCode);
        }
    }
}

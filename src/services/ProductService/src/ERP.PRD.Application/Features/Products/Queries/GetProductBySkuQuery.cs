using MediatR;

namespace ERP.PRD.Application.Features.Products.Queries
{
    public class GetProductBySkuQuery : IRequest<ProductDto>
    {
        public string Sku { get; set; } = string.Empty;
    }

    public class GetProductBySkuHandler : IRequestHandler<GetProductBySkuQuery, ProductDto>
    {
        private readonly IRepository<Domain.Entities.Product> _repository;

        public GetProductBySkuHandler(IRepository<Domain.Entities.Product> repository)
        {
            _repository = repository;
        }

        public async Task<ProductDto> Handle(GetProductBySkuQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Sku))
                throw new ArgumentException("SKU is required");

            var product = await _repository.GetAsync(
                x => x.Sku == request.Sku.ToUpper() && !x.IsDeleted,
                cancellationToken
            );

            if (product == null)
                throw new KeyNotFoundException($"Product with SKU {request.Sku} not found");

            return new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Sku = product.Sku,
                Description = product.Description,
                ProductType = (int)product.ProductType,
                Status = (int)product.Status,
                CategoryId = product.CategoryId,
                HasImage = product.HasImage,
                VariantCount = product.Variants.Count,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
            };
        }
    }
}

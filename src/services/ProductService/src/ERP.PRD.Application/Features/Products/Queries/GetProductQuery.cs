using MediatR;

namespace ERP.PRD.Application.Features.Products.Queries
{
    public class GetProductQuery : IRequest<ProductDto>
    {
        public Guid ProductId { get; set; }
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
        public bool HasImage { get; set; }
        public int VariantCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class GetProductHandler : IRequestHandler<GetProductQuery, ProductDto>
    {
        private readonly IRepository<Domain.Entities.Product> _repository;

        public GetProductHandler(IRepository<Domain.Entities.Product> repository)
        {
            _repository = repository;
        }

        public async Task<ProductDto> Handle(GetProductQuery request, CancellationToken cancellationToken)
        {
            var product = await _repository.GetAsync(
                x => x.Id == request.ProductId && !x.IsDeleted,
                cancellationToken
            );

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

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

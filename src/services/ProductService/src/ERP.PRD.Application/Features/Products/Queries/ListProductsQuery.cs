using MediatR;

namespace ERP.PRD.Application.Features.Products.Queries
{
    public class ListProductsQuery : IRequest<ListProductsResponse>
    {
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 20;
        public Guid? CategoryId { get; set; }
        public int? Status { get; set; }
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
        private readonly IRepository<Domain.Entities.Product> _repository;

        public ListProductsHandler(IRepository<Domain.Entities.Product> repository)
        {
            _repository = repository;
        }

        public async Task<ListProductsResponse> Handle(ListProductsQuery request, CancellationToken cancellationToken)
        {
            var products = await _repository.GetAllAsync(
                x => !x.IsDeleted
                    && (!request.CategoryId.HasValue || x.CategoryId == request.CategoryId)
                    && (!request.Status.HasValue || (int)x.Status == request.Status),
                cancellationToken
            );

            var total = products.Count();
            var pagedProducts = products
                .OrderByDescending(p => p.CreatedAt)
                .Skip(request.Skip)
                .Take(request.Take)
                .ToList();

            return new ListProductsResponse
            {
                Total = total,
                Skip = request.Skip,
                Take = request.Take,
                Products = pagedProducts.Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Sku = p.Sku,
                    Description = p.Description,
                    ProductType = (int)p.ProductType,
                    Status = (int)p.Status,
                    CategoryId = p.CategoryId,
                    HasImage = p.HasImage,
                    VariantCount = p.Variants.Count,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                }).ToList()
            };
        }
    }
}

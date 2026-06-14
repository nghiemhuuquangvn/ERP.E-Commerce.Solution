using MediatR;

namespace ERP.PRD.Application.Features.Products.Queries
{
    public class SearchProductsQuery : IRequest<List<ProductDto>>
    {
        public string SearchTerm { get; set; } = string.Empty;
    }

    public class SearchProductsHandler : IRequestHandler<SearchProductsQuery, List<ProductDto>>
    {
        private readonly IRepository<Domain.Entities.Product> _repository;

        public SearchProductsHandler(IRepository<Domain.Entities.Product> repository)
        {
            _repository = repository;
        }

        public async Task<List<ProductDto>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.SearchTerm))
                return new();

            var searchLower = request.SearchTerm.ToLower();

            var products = await _repository.GetAllAsync(
                x => !x.IsDeleted
                    && (x.Name.ToLower().Contains(searchLower)
                        || x.Sku.ToLower().Contains(searchLower)
                        || (x.Description != null && x.Description.ToLower().Contains(searchLower))),
                cancellationToken
            );

            return products
                .OrderByDescending(p => p.Name.StartsWith(request.SearchTerm, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(p => p.CreatedAt)
                .Take(50)
                .Select(p => new ProductDto
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
                })
                .ToList();
        }
    }
}

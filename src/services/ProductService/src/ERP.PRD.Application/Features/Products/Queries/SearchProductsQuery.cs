using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Products.Queries
{
    public class SearchProductsQuery : IRequest<List<ProductDto>>
    {
        public string SearchTerm { get; set; } = string.Empty;
        public string? LanguageCode { get; set; }
    }

    public class SearchProductsHandler : IRequestHandler<SearchProductsQuery, List<ProductDto>>
    {
        private readonly ProductDbContext _context;

        public SearchProductsHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<List<ProductDto>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.SearchTerm))
                return new();

            var searchLower = request.SearchTerm.ToLower();

            var products = await _context.Products
                .Include(p => p.Variants)
                .Include(p => p.Translations)
                .Where(x => !x.IsDeleted
                    && (x.Name.ToLower().Contains(searchLower)
                        || x.Sku.ToLower().Contains(searchLower)
                        || (x.Description != null && x.Description.ToLower().Contains(searchLower))))
                .ToListAsync(cancellationToken);

            return products
                .OrderByDescending(p => p.Name.StartsWith(request.SearchTerm, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(p => p.CreatedAt)
                .Take(50)
                .Select(p => ProductTranslationMapper.MapToDto(p, request.LanguageCode))
                .ToList();
        }
    }
}

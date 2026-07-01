using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
        private readonly ProductDbContext _context;

        public ListProductsHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<ListProductsResponse> Handle(ListProductsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Products
                .Include(p => p.Variants)
                .Include(p => p.Translations)
                .Where(x => !x.IsDeleted
                    && (!request.CategoryId.HasValue || x.CategoryId == request.CategoryId)
                    && (!request.Status.HasValue || (int)x.Status == request.Status));

            var total = await query.CountAsync(cancellationToken);
            var pagedProducts = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip(request.Skip)
                .Take(request.Take)
                .ToListAsync(cancellationToken);

            return new ListProductsResponse
            {
                Total = total,
                Skip = request.Skip,
                Take = request.Take,
                Products = pagedProducts.Select(p => ProductTranslationMapper.MapToDto(p, request.LanguageCode)).ToList()
            };
        }
    }
}

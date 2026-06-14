using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Categories.Queries
{
    public class GetCategoriesQuery : IRequest<List<CategoryDto>>
    {
        public Guid? ParentCategoryId { get; set; }
    }

    public class CategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? ParentCategoryId { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GetCategoriesHandler : IRequestHandler<GetCategoriesQuery, List<CategoryDto>>
    {
        private readonly ProductDbContext _context;
        private readonly ILogger<GetCategoriesHandler> _logger;

        public GetCategoriesHandler(ProductDbContext context, ILogger<GetCategoriesHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Categories.AsQueryable();

            if (request.ParentCategoryId.HasValue)
                query = query.Where(c => c.ParentCategoryId == request.ParentCategoryId);
            else
                query = query.Where(c => c.ParentCategoryId == null);

            var categories = await query
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    ParentCategoryId = c.ParentCategoryId,
                    SortOrder = c.SortOrder,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Retrieved {Count} categories", categories.Count);
            return categories;
        }
    }
}

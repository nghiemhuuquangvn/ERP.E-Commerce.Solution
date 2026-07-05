using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Categories.Queries
{
    public class GetCategoriesQuery : IRequest<List<CategoryDto>>
    {
        public Guid? ParentCategoryId { get; set; }
        public string? LanguageCode { get; set; }
    }

    public class CategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? ParentCategoryId { get; set; }
        public int SortOrder { get; set; }
        public string LanguageCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class GetCategoriesHandler : IRequestHandler<GetCategoriesQuery, List<CategoryDto>>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<GetCategoriesHandler> _logger;

        public GetCategoriesHandler(IProductUnitOfWork uow, ILogger<GetCategoriesHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task<List<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
        {
            var categories = await _uow.Categories.GetByParentIdAsync(request.ParentCategoryId, cancellationToken);

            var dtos = categories.Select(c =>
            {
                var translation = string.IsNullOrEmpty(request.LanguageCode)
                    ? null
                    : c.Translations.FirstOrDefault(t => t.LanguageCode == request.LanguageCode);

                return new CategoryDto
                {
                    Id = c.Id,
                    Name = translation?.Name ?? c.Name,
                    ParentCategoryId = c.ParentCategoryId,
                    SortOrder = c.SortOrder,
                    LanguageCode = translation != null ? translation.LanguageCode : "default",
                    CreatedAt = c.CreatedAt
                };
            }).ToList();

            _logger.LogInformation("Retrieved {Count} categories", dtos.Count);
            return dtos;
        }
    }
}

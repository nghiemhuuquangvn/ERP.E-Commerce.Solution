using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Attributes.Queries
{
    public class GetProductAttributesQuery : IRequest<List<ProductAttributeDto>>
    {
        public Guid? CategoryId { get; set; }
    }

    public class AttributeOptionDto
    {
        public Guid Id { get; set; }
        public string Value { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    public class ProductAttributeDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int DataType { get; set; }
        public string? Unit { get; set; }
        public Guid? CategoryId { get; set; }
        public bool IsVariantOption { get; set; }
        public bool IsRequired { get; set; }
        public bool IsFilterable { get; set; }
        public int SortOrder { get; set; }
        public List<AttributeOptionDto> Options { get; set; } = new();
    }

    public class GetProductAttributesHandler : IRequestHandler<GetProductAttributesQuery, List<ProductAttributeDto>>
    {
        private readonly ProductDbContext _context;

        public GetProductAttributesHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<List<ProductAttributeDto>> Handle(GetProductAttributesQuery request, CancellationToken cancellationToken)
        {
            var query = _context.ProductAttributes
                .Include(a => a.Options)
                .Where(a => !a.IsDeleted)
                .AsQueryable();

            // Global (CategoryId == null) attributes always apply, plus the requested category's own.
            query = request.CategoryId.HasValue
                ? query.Where(a => a.CategoryId == null || a.CategoryId == request.CategoryId)
                : query.Where(a => a.CategoryId == null);

            var attributes = await query
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.Name)
                .ToListAsync(cancellationToken);

            return attributes.Select(a => new ProductAttributeDto
            {
                Id = a.Id,
                Code = a.Code,
                Name = a.Name,
                DataType = (int)a.DataType,
                Unit = a.Unit,
                CategoryId = a.CategoryId,
                IsVariantOption = a.IsVariantOption,
                IsRequired = a.IsRequired,
                IsFilterable = a.IsFilterable,
                SortOrder = a.SortOrder,
                Options = a.Options
                    .OrderBy(o => o.SortOrder)
                    .Select(o => new AttributeOptionDto { Id = o.Id, Value = o.Value, SortOrder = o.SortOrder })
                    .ToList()
            }).ToList();
        }
    }
}

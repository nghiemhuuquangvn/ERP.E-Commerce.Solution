using ERP.PRD.Domain.Enums;
using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Relations.Queries
{
    public class GetProductRelationsQuery : IRequest<List<ProductRelationDto>>
    {
        public Guid ProductId { get; set; }
        public ProductRelationType? RelationType { get; set; }
    }

    public class ProductRelationDto
    {
        public Guid Id { get; set; }
        public Guid RelatedProductId { get; set; }
        public string RelatedName { get; set; } = string.Empty;
        public string RelatedSku { get; set; } = string.Empty;
        public ProductRelationType RelationType { get; set; }
        public int SortOrder { get; set; }
    }

    public class GetProductRelationsHandler : IRequestHandler<GetProductRelationsQuery, List<ProductRelationDto>>
    {
        private readonly ProductDbContext _context;

        public GetProductRelationsHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<List<ProductRelationDto>> Handle(GetProductRelationsQuery request, CancellationToken cancellationToken)
        {
            var query =
                from relation in _context.ProductRelations
                join related in _context.Products on relation.RelatedProductId equals related.Id
                where relation.ProductId == request.ProductId
                select new { relation, related };

            if (request.RelationType.HasValue)
                query = query.Where(x => x.relation.RelationType == request.RelationType.Value);

            return await query
                .OrderBy(x => x.relation.SortOrder)
                .Select(x => new ProductRelationDto
                {
                    Id = x.relation.Id,
                    RelatedProductId = x.related.Id,
                    RelatedName = x.related.Name,
                    RelatedSku = x.related.Sku,
                    RelationType = x.relation.RelationType,
                    SortOrder = x.relation.SortOrder
                })
                .ToListAsync(cancellationToken);
        }
    }
}

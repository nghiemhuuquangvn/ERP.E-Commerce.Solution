using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Enums;
using MediatR;

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
        private readonly IProductUnitOfWork _uow;

        public GetProductRelationsHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<List<ProductRelationDto>> Handle(GetProductRelationsQuery request, CancellationToken cancellationToken)
        {
            var items = await _uow.Relations.GetByProductIdAsync(
                request.ProductId, request.RelationType, cancellationToken);

            return items.Select(x => new ProductRelationDto
            {
                Id = x.Id,
                RelatedProductId = x.RelatedProductId,
                RelatedName = x.RelatedName,
                RelatedSku = x.RelatedSku,
                RelationType = x.RelationType,
                SortOrder = x.SortOrder
            }).ToList();
        }
    }
}

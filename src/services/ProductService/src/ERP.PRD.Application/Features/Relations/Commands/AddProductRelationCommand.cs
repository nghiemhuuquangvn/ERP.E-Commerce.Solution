using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using MediatR;

namespace ERP.PRD.Application.Features.Relations.Commands
{
    public class AddProductRelationCommand : IRequest<Guid>
    {
        public Guid ProductId { get; set; }
        public Guid RelatedProductId { get; set; }
        public ProductRelationType RelationType { get; set; }
        public int SortOrder { get; set; }
    }

    public class AddProductRelationHandler : IRequestHandler<AddProductRelationCommand, Guid>
    {
        private readonly IProductUnitOfWork _uow;

        public AddProductRelationHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Guid> Handle(AddProductRelationCommand request, CancellationToken cancellationToken)
        {
            var productExists = await _uow.Products.ExistsAsync(request.ProductId, cancellationToken);
            if (!productExists)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var relatedExists = await _uow.Products.ExistsAsync(request.RelatedProductId, cancellationToken);
            if (!relatedExists)
                throw new KeyNotFoundException($"Related product with ID {request.RelatedProductId} not found");

            var relation = ProductRelation.Create(request.ProductId, request.RelatedProductId, request.RelationType, request.SortOrder);
            await _uow.Relations.AddAsync(relation, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            return relation.Id;
        }
    }
}

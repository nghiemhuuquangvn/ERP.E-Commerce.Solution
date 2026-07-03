using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
        private readonly ProductDbContext _context;

        public AddProductRelationHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Guid> Handle(AddProductRelationCommand request, CancellationToken cancellationToken)
        {
            var productExists = await _context.Products.AnyAsync(p => p.Id == request.ProductId && !p.IsDeleted, cancellationToken);
            if (!productExists)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var relatedExists = await _context.Products.AnyAsync(p => p.Id == request.RelatedProductId && !p.IsDeleted, cancellationToken);
            if (!relatedExists)
                throw new KeyNotFoundException($"Related product with ID {request.RelatedProductId} not found");

            var relation = ProductRelation.Create(request.ProductId, request.RelatedProductId, request.RelationType, request.SortOrder);
            await _context.ProductRelations.AddAsync(relation, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return relation.Id;
        }
    }
}

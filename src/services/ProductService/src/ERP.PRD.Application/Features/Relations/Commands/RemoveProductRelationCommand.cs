using ERP.PRD.Infrastructure.Persistence;
using MediatR;

namespace ERP.PRD.Application.Features.Relations.Commands
{
    public class RemoveProductRelationCommand : IRequest<Unit>
    {
        public Guid RelationId { get; set; }
    }

    public class RemoveProductRelationHandler : IRequestHandler<RemoveProductRelationCommand, Unit>
    {
        private readonly ProductDbContext _context;

        public RemoveProductRelationHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(RemoveProductRelationCommand request, CancellationToken cancellationToken)
        {
            var relation = await _context.ProductRelations.FindAsync(new object[] { request.RelationId }, cancellationToken);
            if (relation == null)
                throw new KeyNotFoundException($"Relation with ID {request.RelationId} not found");

            _context.ProductRelations.Remove(relation);
            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

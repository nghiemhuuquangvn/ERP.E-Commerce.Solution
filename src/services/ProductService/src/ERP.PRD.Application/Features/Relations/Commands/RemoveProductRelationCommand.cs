using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Relations.Commands
{
    public class RemoveProductRelationCommand : IRequest<Unit>
    {
        public Guid RelationId { get; set; }
    }

    public class RemoveProductRelationHandler : IRequestHandler<RemoveProductRelationCommand, Unit>
    {
        private readonly IProductUnitOfWork _uow;

        public RemoveProductRelationHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Unit> Handle(RemoveProductRelationCommand request, CancellationToken cancellationToken)
        {
            var relation = await _uow.Relations.FindByIdAsync(request.RelationId, cancellationToken);
            if (relation == null)
                throw new KeyNotFoundException($"Relation with ID {request.RelationId} not found");

            _uow.Relations.Remove(relation);
            await _uow.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

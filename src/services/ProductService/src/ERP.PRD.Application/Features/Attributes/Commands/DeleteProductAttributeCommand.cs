using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Attributes.Commands
{
    public class DeleteProductAttributeCommand : IRequest<Unit>
    {
        public Guid Id { get; set; }
    }

    public class DeleteProductAttributeHandler : IRequestHandler<DeleteProductAttributeCommand, Unit>
    {
        private readonly IProductUnitOfWork _uow;

        public DeleteProductAttributeHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Unit> Handle(DeleteProductAttributeCommand request, CancellationToken cancellationToken)
        {
            var attribute = await _uow.Attributes.GetByIdAsync(request.Id, cancellationToken);

            if (attribute == null)
                throw new KeyNotFoundException($"Attribute with ID {request.Id} not found");

            // Mirror Product.RemoveVariant's guard-rail style: refuse the operation outright
            // rather than cascading, since values already assigned to products/variants would
            // otherwise be silently orphaned.
            var hasValues = await _uow.AttributeValues.AnyByAttributeIdAsync(request.Id, cancellationToken);

            if (hasValues)
                throw new InvalidOperationException("Cannot delete an attribute that has values assigned to products or variants. Remove those values first.");

            attribute.Delete();

            await _uow.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

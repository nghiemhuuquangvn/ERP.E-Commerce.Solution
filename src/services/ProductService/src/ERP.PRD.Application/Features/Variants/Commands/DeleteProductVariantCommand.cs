using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Variants.Commands
{
    public class DeleteProductVariantCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
        public Guid VariantId { get; set; }
    }

    public class DeleteProductVariantHandler : IRequestHandler<DeleteProductVariantCommand, Unit>
    {
        private readonly IProductUnitOfWork _uow;

        public DeleteProductVariantHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Unit> Handle(DeleteProductVariantCommand request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdWithVariantsAsync(request.ProductId, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            // Check if this is the last variant (cannot remove - product must have at least one)
            if (product.Variants.Count == 1)
                throw new InvalidOperationException("Cannot delete the last variant. Product must have at least one variant.");

            // Remove variant
            product.RemoveVariant(request.VariantId);

            await _uow.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

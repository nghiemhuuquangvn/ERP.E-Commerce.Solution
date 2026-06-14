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
        private readonly IRepository<Domain.Entities.Product> _productRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteProductVariantHandler(IRepository<Domain.Entities.Product> productRepository, IUnitOfWork unitOfWork)
        {
            _productRepository = productRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(DeleteProductVariantCommand request, CancellationToken cancellationToken)
        {
            var product = await _productRepository.GetAsync(
                x => x.Id == request.ProductId && !x.IsDeleted,
                cancellationToken
            );

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            // Check if this is the last variant (cannot remove - product must have at least one)
            if (product.Variants.Count == 1)
                throw new InvalidOperationException("Cannot delete the last variant. Product must have at least one variant.");

            // Remove variant
            product.RemoveVariant(request.VariantId);

            await _productRepository.UpdateAsync(product, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

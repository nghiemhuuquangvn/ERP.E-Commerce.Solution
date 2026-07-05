using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Products.Commands
{
    public class SubmitForReviewCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
    }

    public class SubmitForReviewHandler : IRequestHandler<SubmitForReviewCommand, Unit>
    {
        private readonly IProductUnitOfWork _uow;

        public SubmitForReviewHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Unit> Handle(SubmitForReviewCommand request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdAsync(request.ProductId, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            product.SubmitForReview();

            await _uow.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

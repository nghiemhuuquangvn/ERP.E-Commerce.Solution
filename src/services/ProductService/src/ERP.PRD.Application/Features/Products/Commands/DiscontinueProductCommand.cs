using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Products.Commands
{
    public class DiscontinueProductCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
    }

    public class DiscontinueProductHandler : IRequestHandler<DiscontinueProductCommand, Unit>
    {
        private readonly IProductUnitOfWork _uow;

        public DiscontinueProductHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Unit> Handle(DiscontinueProductCommand request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdAsync(request.ProductId, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            product.Discontinue();

            await _uow.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

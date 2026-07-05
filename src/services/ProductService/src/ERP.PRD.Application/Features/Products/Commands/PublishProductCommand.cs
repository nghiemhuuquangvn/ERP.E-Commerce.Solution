using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Products.Commands
{
    public class PublishProductCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
    }

    public class PublishProductHandler : IRequestHandler<PublishProductCommand, Unit>
    {
        private readonly IProductUnitOfWork _uow;

        public PublishProductHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Unit> Handle(PublishProductCommand request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdWithVariantsAsync(request.ProductId, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            product.Publish();

            await _uow.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

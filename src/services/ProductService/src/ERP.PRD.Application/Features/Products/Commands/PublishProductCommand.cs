using MediatR;

namespace ERP.PRD.Application.Features.Products.Commands
{
    public class PublishProductCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
    }

    public class PublishProductHandler : IRequestHandler<PublishProductCommand, Unit>
    {
        private readonly IRepository<Domain.Entities.Product> _repository;
        private readonly IUnitOfWork _unitOfWork;

        public PublishProductHandler(IRepository<Domain.Entities.Product> repository, IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(PublishProductCommand request, CancellationToken cancellationToken)
        {
            var product = await _repository.GetAsync(
                x => x.Id == request.ProductId && !x.IsDeleted,
                cancellationToken
            );

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            product.Publish();

            await _repository.UpdateAsync(product, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

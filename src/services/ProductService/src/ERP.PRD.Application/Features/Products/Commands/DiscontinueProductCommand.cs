using MediatR;

namespace ERP.PRD.Application.Features.Products.Commands
{
    public class DiscontinueProductCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
    }

    public class DiscontinueProductHandler : IRequestHandler<DiscontinueProductCommand, Unit>
    {
        private readonly IRepository<Domain.Entities.Product> _repository;
        private readonly IUnitOfWork _unitOfWork;

        public DiscontinueProductHandler(IRepository<Domain.Entities.Product> repository, IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(DiscontinueProductCommand request, CancellationToken cancellationToken)
        {
            var product = await _repository.GetAsync(
                x => x.Id == request.ProductId && !x.IsDeleted,
                cancellationToken
            );

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            product.Discontinue();

            await _repository.UpdateAsync(product, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

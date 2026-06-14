using MediatR;

namespace ERP.PRD.Application.Features.Products.Commands
{
    public class UpdateProductDescriptionCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class UpdateProductDescriptionValidator : FluentValidation.AbstractValidator<UpdateProductDescriptionCommand>
    {
        public UpdateProductDescriptionValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEmpty().WithMessage("Product ID is required");

            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Description is required")
                .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters");
        }
    }

    public class UpdateProductDescriptionHandler : IRequestHandler<UpdateProductDescriptionCommand, Unit>
    {
        private readonly IRepository<Domain.Entities.Product> _repository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateProductDescriptionHandler(IRepository<Domain.Entities.Product> repository, IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(UpdateProductDescriptionCommand request, CancellationToken cancellationToken)
        {
            var product = await _repository.GetAsync(
                x => x.Id == request.ProductId && !x.IsDeleted,
                cancellationToken
            );

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            product.UpdateDescription(request.Description);

            await _repository.UpdateAsync(product, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

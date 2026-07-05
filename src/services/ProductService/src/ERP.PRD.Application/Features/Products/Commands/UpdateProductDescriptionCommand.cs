using ERP.PRD.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Products.Commands
{
    public class UpdateProductDescriptionCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class UpdateProductDescriptionValidator : AbstractValidator<UpdateProductDescriptionCommand>
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
        private readonly IProductUnitOfWork _uow;

        public UpdateProductDescriptionHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Unit> Handle(UpdateProductDescriptionCommand request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdAsync(request.ProductId, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            product.UpdateDescription(request.Description);

            await _uow.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

using ERP.PRD.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Products.Commands
{
    public class SetProductTranslationCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
        public string LanguageCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class SetProductTranslationValidator : AbstractValidator<SetProductTranslationCommand>
    {
        public SetProductTranslationValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEmpty().WithMessage("Product ID is required");

            RuleFor(x => x.LanguageCode)
                .NotEmpty().WithMessage("Language code is required")
                .MaximumLength(10).WithMessage("Language code cannot exceed 10 characters");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required")
                .MaximumLength(255).WithMessage("Name cannot exceed 255 characters");

            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters");
        }
    }

    public class SetProductTranslationHandler : IRequestHandler<SetProductTranslationCommand, Unit>
    {
        private readonly IProductUnitOfWork _uow;

        public SetProductTranslationHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Unit> Handle(SetProductTranslationCommand request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdWithTranslationsAsync(request.ProductId, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            product.SetTranslation(request.LanguageCode, request.Name, request.Description);

            await _uow.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

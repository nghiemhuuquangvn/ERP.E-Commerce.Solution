using ERP.PRD.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
        private readonly ProductDbContext _context;

        public SetProductTranslationHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(SetProductTranslationCommand request, CancellationToken cancellationToken)
        {
            var product = await _context.Products
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(x => x.Id == request.ProductId && !x.IsDeleted, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            product.SetTranslation(request.LanguageCode, request.Name, request.Description);

            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

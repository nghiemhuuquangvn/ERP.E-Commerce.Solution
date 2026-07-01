using ERP.PRD.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Categories.Commands
{
    public class SetCategoryTranslationCommand : IRequest<Unit>
    {
        public Guid CategoryId { get; set; }
        public string LanguageCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class SetCategoryTranslationValidator : AbstractValidator<SetCategoryTranslationCommand>
    {
        public SetCategoryTranslationValidator()
        {
            RuleFor(x => x.CategoryId)
                .NotEmpty().WithMessage("Category ID is required");

            RuleFor(x => x.LanguageCode)
                .NotEmpty().WithMessage("Language code is required")
                .MaximumLength(10).WithMessage("Language code cannot exceed 10 characters");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required")
                .MaximumLength(100).WithMessage("Name cannot exceed 100 characters");
        }
    }

    public class SetCategoryTranslationHandler : IRequestHandler<SetCategoryTranslationCommand, Unit>
    {
        private readonly ProductDbContext _context;
        private readonly ILogger<SetCategoryTranslationHandler> _logger;

        public SetCategoryTranslationHandler(ProductDbContext context, ILogger<SetCategoryTranslationHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Unit> Handle(SetCategoryTranslationCommand request, CancellationToken cancellationToken)
        {
            var category = await _context.Categories
                .Include(c => c.Translations)
                .FirstOrDefaultAsync(x => x.Id == request.CategoryId, cancellationToken);

            if (category == null)
                throw new KeyNotFoundException($"Category with ID {request.CategoryId} not found");

            category.SetTranslation(request.LanguageCode, request.Name);
            category.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Category translation set: {CategoryId}, Language: {LanguageCode}", category.Id, request.LanguageCode);

            return Unit.Value;
        }
    }
}

using ERP.PRD.Infrastructure.Persistence;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Categories.Commands
{
    public class UpdateCategoryCommand : IRequest
    {
        public Guid CategoryId { get; set; }
        public string? Name { get; set; }
        public Guid? ParentCategoryId { get; set; }
    }

    public class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
    {
        public UpdateCategoryValidator()
        {
            RuleFor(x => x.CategoryId)
                .NotEqual(Guid.Empty).WithMessage("Category ID is required");

            RuleFor(x => x.Name)
                .MaximumLength(100).WithMessage("Category name cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Name));

            RuleFor(x => x.ParentCategoryId)
                .NotEqual(Guid.Empty).WithMessage("Invalid parent category ID").When(x => x.ParentCategoryId.HasValue);
        }
    }

    public class UpdateCategoryHandler : IRequestHandler<UpdateCategoryCommand>
    {
        private readonly ProductDbContext _context;
        private readonly ILogger<UpdateCategoryHandler> _logger;

        public UpdateCategoryHandler(ProductDbContext context, ILogger<UpdateCategoryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
        {
            var category = await _context.Categories.FindAsync(new object[] { request.CategoryId }, cancellationToken: cancellationToken);
            if (category == null)
                throw new KeyNotFoundException($"Category with ID {request.CategoryId} not found");

            if (!string.IsNullOrEmpty(request.Name))
                category.Name = request.Name;

            if (request.ParentCategoryId.HasValue)
            {
                var parentExists = await _context.Categories.FindAsync(new object[] { request.ParentCategoryId }, cancellationToken: cancellationToken);
                if (parentExists == null)
                    throw new KeyNotFoundException($"Parent category with ID {request.ParentCategoryId} not found");

                category.ParentCategoryId = request.ParentCategoryId;
            }

            category.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Category updated: {CategoryId}", request.CategoryId);
        }
    }
}

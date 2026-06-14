using ERP.PRD.Domain.Entities;
using ERP.PRD.Infrastructure.Persistence;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Categories.Commands
{
    public class CreateCategoryCommand : IRequest<CreateCategoryResponse>
    {
        public string Name { get; set; } = string.Empty;
        public Guid? ParentCategoryId { get; set; }
    }

    public class CreateCategoryResponse
    {
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
    {
        public CreateCategoryValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Category name is required")
                .MaximumLength(100).WithMessage("Category name cannot exceed 100 characters");

            RuleFor(x => x.ParentCategoryId)
                .NotEqual(Guid.Empty).WithMessage("Invalid parent category ID").When(x => x.ParentCategoryId.HasValue);
        }
    }

    public class CreateCategoryHandler : IRequestHandler<CreateCategoryCommand, CreateCategoryResponse>
    {
        private readonly ProductDbContext _context;
        private readonly ILogger<CreateCategoryHandler> _logger;

        public CreateCategoryHandler(ProductDbContext context, ILogger<CreateCategoryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CreateCategoryResponse> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
        {
            if (request.ParentCategoryId.HasValue)
            {
                var parentExists = await _context.Categories.FindAsync(new object[] { request.ParentCategoryId }, cancellationToken: cancellationToken);
                if (parentExists == null)
                    throw new KeyNotFoundException($"Parent category with ID {request.ParentCategoryId} not found");
            }

            var category = Category.Create(request.Name, request.ParentCategoryId);

            _context.Categories.Add(category);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Category created: {CategoryId}, Name: {Name}", category.Id, category.Name);

            return new CreateCategoryResponse
            {
                CategoryId = category.Id,
                Name = category.Name
            };
        }
    }
}

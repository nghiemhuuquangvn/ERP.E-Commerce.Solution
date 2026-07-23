using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Categories.Commands
{
    public class CreateCategoryCommand : IRequest<CreateCategoryResponse>
    {
        public string Name { get; set; } = string.Empty;
        public Guid? ParentCategoryId { get; set; }
        public string? Code { get; set; }
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

            RuleFor(x => x.Code)
                .MaximumLength(50).WithMessage("Category code cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.Code));
        }
    }

    public class CreateCategoryHandler : IRequestHandler<CreateCategoryCommand, CreateCategoryResponse>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<CreateCategoryHandler> _logger;

        public CreateCategoryHandler(IProductUnitOfWork uow, ILogger<CreateCategoryHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task<CreateCategoryResponse> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
        {
            if (request.ParentCategoryId.HasValue)
            {
                var parentExists = await _uow.Categories.ExistsAsync(request.ParentCategoryId.Value, cancellationToken);
                if (!parentExists)
                    throw new KeyNotFoundException($"Parent category with ID {request.ParentCategoryId} not found");
            }

            var category = Category.Create(request.Name, request.ParentCategoryId, request.Code);

            await _uow.Categories.AddAsync(category, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Category created: {CategoryId}, Name: {Name}", category.Id, category.Name);

            return new CreateCategoryResponse
            {
                CategoryId = category.Id,
                Name = category.Name
            };
        }
    }
}

using ERP.PRD.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Categories.Commands
{
    public class UpdateCategoryCommand : IRequest
    {
        public Guid CategoryId { get; set; }
        public string? Name { get; set; }
        public Guid? ParentCategoryId { get; set; }
        public string? Code { get; set; }
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

            RuleFor(x => x.Code)
                .MaximumLength(50).WithMessage("Category code cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.Code));
        }
    }

    public class UpdateCategoryHandler : IRequestHandler<UpdateCategoryCommand>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<UpdateCategoryHandler> _logger;

        public UpdateCategoryHandler(IProductUnitOfWork uow, ILogger<UpdateCategoryHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
        {
            var category = await _uow.Categories.GetByIdAsync(request.CategoryId, cancellationToken);
            if (category == null)
                throw new KeyNotFoundException($"Category with ID {request.CategoryId} not found");

            if (!string.IsNullOrEmpty(request.Name))
                category.Name = request.Name;

            if (request.ParentCategoryId.HasValue)
            {
                var parentExists = await _uow.Categories.ExistsAsync(request.ParentCategoryId.Value, cancellationToken);
                if (!parentExists)
                    throw new KeyNotFoundException($"Parent category with ID {request.ParentCategoryId} not found");

                category.ParentCategoryId = request.ParentCategoryId;
            }

            if (!string.IsNullOrEmpty(request.Code))
                category.SetCode(request.Code);

            category.UpdatedAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Category updated: {CategoryId}", request.CategoryId);
        }
    }
}

using FluentValidation;

namespace ERP.PRD.Application.Features.Products.Commands
{
    public class CreateProductValidator : AbstractValidator<CreateProductCommand>
    {
        public CreateProductValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Product name is required")
                .MaximumLength(255).WithMessage("Product name cannot exceed 255 characters")
                .MinimumLength(3).WithMessage("Product name must be at least 3 characters");

            RuleFor(x => x.Sku)
                .NotEmpty().WithMessage("SKU is required")
                .MaximumLength(50).WithMessage("SKU cannot exceed 50 characters")
                .Matches(@"^[A-Z0-9\-]+$").WithMessage("SKU must contain only uppercase letters, numbers, and hyphens");

            RuleFor(x => x.ProductType)
                .GreaterThan(0).WithMessage("Valid product type is required")
                .LessThan(4).WithMessage("Product type must be Physical, Digital, Service, or Bundle");

            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters");

            RuleFor(x => x.CategoryId)
                .NotEmpty().When(x => x.CategoryId.HasValue).WithMessage("Category ID must be valid if provided");
        }
    }
}

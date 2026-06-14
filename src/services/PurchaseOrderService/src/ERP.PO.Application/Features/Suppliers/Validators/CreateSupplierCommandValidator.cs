using FluentValidation;
using ERP.PO.Application.Features.Suppliers.Commands;

namespace ERP.PO.Application.Features.Suppliers.Validators;

public class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Supplier Code is required.")
            .MaximumLength(20).WithMessage("Supplier Code must not exceed 20 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Supplier Name is required.")
            .MaximumLength(100).WithMessage("Supplier Name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("Phone must not exceed 20 characters.");
    }
}

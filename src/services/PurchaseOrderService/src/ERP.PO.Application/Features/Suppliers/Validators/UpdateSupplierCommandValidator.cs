using FluentValidation;
using ERP.PO.Application.Features.Suppliers.Commands;

namespace ERP.PO.Application.Features.Suppliers.Validators;

public class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("Supplier ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Supplier Name is required.")
            .MaximumLength(100).WithMessage("Supplier Name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("Phone must not exceed 20 characters.");

        RuleFor(x => x.UpdatedBy)
            .NotEmpty().WithMessage("UpdatedBy is required.");

        RuleFor(x => x.TaxCode)
            .MaximumLength(50).WithMessage("Tax code must not exceed 50 characters.");

        RuleFor(x => x.CurrencyCode)
            .Length(3).WithMessage("Currency code must be a 3-letter ISO code.")
            .When(x => !string.IsNullOrEmpty(x.CurrencyCode));
    }
}

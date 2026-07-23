using FluentValidation;
using ERP.PO.Application.Features.Suppliers.Commands;

namespace ERP.PO.Application.Features.Suppliers.Validators;

public class UpdateSupplierItemCommandValidator : AbstractValidator<UpdateSupplierItemCommand>
{
    public UpdateSupplierItemCommandValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("Supplier ID is required.");

        RuleFor(x => x.SupplierItemId)
            .NotEmpty().WithMessage("Supplier item ID is required.");

        RuleFor(x => x.SupplierItemCode)
            .MaximumLength(50).WithMessage("Supplier item code must not exceed 50 characters.");

        RuleFor(x => x.LastPurchasePrice)
            .GreaterThanOrEqualTo(0).WithMessage("Last purchase price cannot be negative.");

        RuleFor(x => x.LeadTimeDays)
            .GreaterThanOrEqualTo(0).WithMessage("Lead time cannot be negative.");
    }
}

using FluentValidation;
using ERP.PO.Application.Features.PurchaseOrders.Commands;

namespace ERP.PO.Application.Features.PurchaseOrders.Validators;

public class AddItemCommandValidator : AbstractValidator<AddItemCommand>
{
    public AddItemCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId)
            .NotEmpty().WithMessage("Purchase Order ID is required.");

        RuleFor(x => x.ProductCode)
            .NotEmpty().WithMessage("Product Code is required.")
            .MaximumLength(50).WithMessage("Product Code must not exceed 50 characters.");

        RuleFor(x => x.ProductName)
            .NotEmpty().WithMessage("Product Name is required.")
            .MaximumLength(100).WithMessage("Product Name must not exceed 100 characters.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Unit is required.")
            .MaximumLength(20).WithMessage("Unit must not exceed 20 characters.");

        RuleFor(x => x.UnitPrice)
            .GreaterThan(0).WithMessage("Unit Price must be greater than zero.");

        RuleFor(x => x.AddedBy)
            .NotEmpty().WithMessage("AddedBy is required.");
    }
}

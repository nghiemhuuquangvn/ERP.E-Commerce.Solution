using FluentValidation;
using ERP.PO.Application.Features.PurchaseOrders.Commands;
using ERP.Shared.Authorization;

namespace ERP.PO.Application.Features.PurchaseOrders.Validators;

public class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    private readonly IUserContextService _userContextService;

    public CreatePurchaseOrderCommandValidator(IUserContextService userContextService)
    {
        _userContextService = userContextService;

        RuleFor(x => x.PONumber)
            .NotEmpty().WithMessage("PO Number is required.")
            .MaximumLength(50).WithMessage("PO Number must not exceed 50 characters.");

        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("Supplier ID is required.");

        RuleFor(x => x.ExpectedDeliveryDate)
            .GreaterThan(DateTime.UtcNow).WithMessage("Expected delivery date must be in the future.")
            .When(x => x.ExpectedDeliveryDate.HasValue);
    }
}

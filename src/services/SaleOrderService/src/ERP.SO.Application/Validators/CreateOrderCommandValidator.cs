using ERP.SO.Application.Commands;
using FluentValidation;

namespace ERP.SO.Application.Validators
{
    public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
    {
        public CreateOrderCommandValidator()
        {
            RuleFor(x => x.CustomerId)
                .NotEmpty().WithMessage("Customer ID is required");

            RuleFor(x => x.CustomerName)
                .NotEmpty().WithMessage("Customer name is required")
                .MaximumLength(200).WithMessage("Customer name cannot exceed 200 characters");

            RuleFor(x => x.CustomerEmail)
                .NotEmpty().WithMessage("Customer email is required")
                .EmailAddress().WithMessage("Customer email must be valid");

            RuleFor(x => x.LineItems)
                .NotEmpty().WithMessage("Order must have at least one line item");

            RuleForEach(x => x.LineItems)
                .ChildRules(item =>
                {
                    item.RuleFor(i => i.ProductId).NotEmpty();
                    item.RuleFor(i => i.Quantity).GreaterThan(0);
                    item.RuleFor(i => i.UnitPrice).GreaterThan(0);
                });

            RuleFor(x => x.ShippingAddress)
                .NotNull().WithMessage("Shipping address is required")
                .ChildRules(address =>
                {
                    address.RuleFor(a => a.Street).NotEmpty();
                    address.RuleFor(a => a.City).NotEmpty();
                    address.RuleFor(a => a.State).NotEmpty();
                    address.RuleFor(a => a.PostalCode).NotEmpty();
                });
        }
    }

    public class ApproveOrderCommandValidator : AbstractValidator<ApproveOrderCommand>
    {
        public ApproveOrderCommandValidator()
        {
            RuleFor(x => x.OrderId).NotEmpty().WithMessage("Order ID is required");
        }
    }

    public class ShipOrderCommandValidator : AbstractValidator<ShipOrderCommand>
    {
        public ShipOrderCommandValidator()
        {
            RuleFor(x => x.OrderId).NotEmpty();
            RuleFor(x => x.TrackingNumber).NotEmpty();
            RuleFor(x => x.Carrier).NotEmpty();
        }
    }

    public class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
    {
        public CancelOrderCommandValidator()
        {
            RuleFor(x => x.OrderId).NotEmpty();
            RuleFor(x => x.Reason).NotEmpty();
        }
    }
}

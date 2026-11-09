using ERP.PRD.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Products.Commands
{
    /// <summary>Sets whether the item is tracked by lot and / or serial number (read by the Warehouse and Manufacturing through the product lookup).</summary>
    public class SetProductTrackingCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
        public bool IsLotControlled { get; set; }
        public bool IsSerialControlled { get; set; }
    }

    public class SetProductTrackingValidator : AbstractValidator<SetProductTrackingCommand>
    {
        public SetProductTrackingValidator()
        {
            RuleFor(x => x.ProductId).NotEqual(Guid.Empty).WithMessage("Product ID is required");
        }
    }

    public class SetProductTrackingHandler : IRequestHandler<SetProductTrackingCommand, Unit>
    {
        private readonly IProductUnitOfWork _uow;

        public SetProductTrackingHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Unit> Handle(SetProductTrackingCommand request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdAsync(request.ProductId, cancellationToken)
                ?? throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            product.SetTracking(request.IsLotControlled, request.IsSerialControlled);
            await _uow.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}

using ERP.PRD.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Products.Commands
{
    /// <summary>
    /// Sets the product's base unit of measure. BaseUomId references
    /// SharedMasterDataService's UnitOfMeasure by Id only (cross-service, no
    /// database FK — see CLAUDE.md database ownership rules), so this handler
    /// deliberately does not validate the Id against another service's database.
    /// </summary>
    public class SetProductBaseUomCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
        public Guid? BaseUomId { get; set; }
    }

    public class SetProductBaseUomValidator : AbstractValidator<SetProductBaseUomCommand>
    {
        public SetProductBaseUomValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEqual(Guid.Empty).WithMessage("Product ID is required");
        }
    }

    public class SetProductBaseUomHandler : IRequestHandler<SetProductBaseUomCommand, Unit>
    {
        private readonly IProductUnitOfWork _uow;

        public SetProductBaseUomHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Unit> Handle(SetProductBaseUomCommand request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdAsync(request.ProductId, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            product.SetBaseUom(request.BaseUomId);

            await _uow.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

using ERP.PRD.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Products.Commands
{
    public class SetProductBrandCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
        public Guid? BrandId { get; set; }
    }

    public class SetProductBrandValidator : AbstractValidator<SetProductBrandCommand>
    {
        public SetProductBrandValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEqual(Guid.Empty).WithMessage("Product ID is required");
        }
    }

    public class SetProductBrandHandler : IRequestHandler<SetProductBrandCommand, Unit>
    {
        private readonly IProductUnitOfWork _uow;

        public SetProductBrandHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Unit> Handle(SetProductBrandCommand request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdAsync(request.ProductId, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            if (request.BrandId.HasValue)
            {
                var brandExists = await _uow.Brands.ExistsAsync(request.BrandId.Value, cancellationToken);
                if (!brandExists)
                    throw new KeyNotFoundException($"Brand with ID {request.BrandId} not found");
            }

            product.SetBrand(request.BrandId);

            await _uow.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

using ERP.PRD.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Brands.Commands
{
    public class DeactivateBrandCommand : IRequest
    {
        public Guid BrandId { get; set; }
    }

    public class DeactivateBrandValidator : AbstractValidator<DeactivateBrandCommand>
    {
        public DeactivateBrandValidator()
        {
            RuleFor(x => x.BrandId)
                .NotEqual(Guid.Empty).WithMessage("Brand ID is required");
        }
    }

    public class DeactivateBrandHandler : IRequestHandler<DeactivateBrandCommand>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<DeactivateBrandHandler> _logger;

        public DeactivateBrandHandler(IProductUnitOfWork uow, ILogger<DeactivateBrandHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task Handle(DeactivateBrandCommand request, CancellationToken cancellationToken)
        {
            var brand = await _uow.Brands.GetByIdAsync(request.BrandId, cancellationToken);
            if (brand == null)
                throw new KeyNotFoundException($"Brand with ID {request.BrandId} not found");

            brand.Deactivate();
            _uow.Brands.Update(brand);

            await _uow.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Brand deactivated: {BrandId}", request.BrandId);
        }
    }
}

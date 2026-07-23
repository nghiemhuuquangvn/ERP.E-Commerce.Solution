using ERP.PRD.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Brands.Commands
{
    public class ActivateBrandCommand : IRequest
    {
        public Guid BrandId { get; set; }
    }

    public class ActivateBrandValidator : AbstractValidator<ActivateBrandCommand>
    {
        public ActivateBrandValidator()
        {
            RuleFor(x => x.BrandId)
                .NotEqual(Guid.Empty).WithMessage("Brand ID is required");
        }
    }

    public class ActivateBrandHandler : IRequestHandler<ActivateBrandCommand>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<ActivateBrandHandler> _logger;

        public ActivateBrandHandler(IProductUnitOfWork uow, ILogger<ActivateBrandHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task Handle(ActivateBrandCommand request, CancellationToken cancellationToken)
        {
            var brand = await _uow.Brands.GetByIdAsync(request.BrandId, cancellationToken);
            if (brand == null)
                throw new KeyNotFoundException($"Brand with ID {request.BrandId} not found");

            brand.Activate();
            _uow.Brands.Update(brand);

            await _uow.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Brand activated: {BrandId}", request.BrandId);
        }
    }
}

using ERP.PRD.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Brands.Commands
{
    public class UpdateBrandCommand : IRequest
    {
        public Guid BrandId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
    }

    public class UpdateBrandValidator : AbstractValidator<UpdateBrandCommand>
    {
        public UpdateBrandValidator()
        {
            RuleFor(x => x.BrandId)
                .NotEqual(Guid.Empty).WithMessage("Brand ID is required");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Brand name is required")
                .MaximumLength(200).WithMessage("Brand name cannot exceed 200 characters");

            RuleFor(x => x.LogoUrl)
                .MaximumLength(500).WithMessage("Logo URL cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.LogoUrl));
        }
    }

    public class UpdateBrandHandler : IRequestHandler<UpdateBrandCommand>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<UpdateBrandHandler> _logger;

        public UpdateBrandHandler(IProductUnitOfWork uow, ILogger<UpdateBrandHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task Handle(UpdateBrandCommand request, CancellationToken cancellationToken)
        {
            var brand = await _uow.Brands.GetByIdAsync(request.BrandId, cancellationToken);
            if (brand == null)
                throw new KeyNotFoundException($"Brand with ID {request.BrandId} not found");

            brand.Update(request.Name, request.LogoUrl);
            _uow.Brands.Update(brand);

            await _uow.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Brand updated: {BrandId}", request.BrandId);
        }
    }
}

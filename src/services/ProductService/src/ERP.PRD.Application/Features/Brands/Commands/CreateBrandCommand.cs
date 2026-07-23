using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Brands.Commands
{
    public class CreateBrandCommand : IRequest<CreateBrandResponse>
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
    }

    public class CreateBrandResponse
    {
        public Guid BrandId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class CreateBrandValidator : AbstractValidator<CreateBrandCommand>
    {
        public CreateBrandValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("Brand code is required")
                .MaximumLength(50).WithMessage("Brand code cannot exceed 50 characters");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Brand name is required")
                .MaximumLength(200).WithMessage("Brand name cannot exceed 200 characters");

            RuleFor(x => x.LogoUrl)
                .MaximumLength(500).WithMessage("Logo URL cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.LogoUrl));
        }
    }

    public class CreateBrandHandler : IRequestHandler<CreateBrandCommand, CreateBrandResponse>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<CreateBrandHandler> _logger;

        public CreateBrandHandler(IProductUnitOfWork uow, ILogger<CreateBrandHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task<CreateBrandResponse> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
        {
            var codeExists = await _uow.Brands.CodeExistsAsync(request.Code, cancellationToken);
            if (codeExists)
                throw new InvalidOperationException($"Brand code '{request.Code}' already exists");

            var brand = Brand.Create(request.Code, request.Name, request.LogoUrl);

            await _uow.Brands.AddAsync(brand, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Brand created: {BrandId}, Code: {Code}", brand.Id, brand.Code);

            return new CreateBrandResponse
            {
                BrandId = brand.Id,
                Code = brand.Code,
                Name = brand.Name
            };
        }
    }
}

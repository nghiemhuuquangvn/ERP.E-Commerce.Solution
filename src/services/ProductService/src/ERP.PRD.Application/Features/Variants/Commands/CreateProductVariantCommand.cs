using MediatR;
using FluentValidation;

namespace ERP.PRD.Application.Features.Variants.Commands
{
    public class CreateProductVariantCommand : IRequest<CreateProductVariantResponse>
    {
        public Guid ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public decimal Cost { get; set; }
        public decimal? Weight { get; set; }
        public string? Dimensions { get; set; }
    }

    public class CreateProductVariantResponse
    {
        public Guid VariantId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public decimal Cost { get; set; }
    }

    public class CreateProductVariantValidator : AbstractValidator<CreateProductVariantCommand>
    {
        public CreateProductVariantValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEmpty().WithMessage("Product ID is required");

            RuleFor(x => x.Sku)
                .NotEmpty().WithMessage("SKU is required")
                .MaximumLength(50).WithMessage("SKU cannot exceed 50 characters")
                .Matches(@"^[A-Z0-9\-]+$").WithMessage("SKU must contain only uppercase letters, numbers, and hyphens");

            RuleFor(x => x.Barcode)
                .MaximumLength(100).WithMessage("Barcode cannot exceed 100 characters");

            RuleFor(x => x.Cost)
                .GreaterThanOrEqualTo(0).WithMessage("Cost must be zero or positive")
                .LessThan(1000000).WithMessage("Cost exceeds maximum allowed value");

            RuleFor(x => x.Weight)
                .GreaterThanOrEqualTo(0).When(x => x.Weight.HasValue).WithMessage("Weight must be positive");
        }
    }

    public class CreateProductVariantHandler : IRequestHandler<CreateProductVariantCommand, CreateProductVariantResponse>
    {
        private readonly IRepository<Domain.Entities.Product> _productRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateProductVariantHandler(IRepository<Domain.Entities.Product> productRepository, IUnitOfWork unitOfWork)
        {
            _productRepository = productRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<CreateProductVariantResponse> Handle(CreateProductVariantCommand request, CancellationToken cancellationToken)
        {
            // Verify product exists
            var product = await _productRepository.GetAsync(
                x => x.Id == request.ProductId && !x.IsDeleted,
                cancellationToken
            );

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            // Check if variant with same SKU already exists
            if (product.GetVariantBySku(request.Sku) != null)
                throw new InvalidOperationException($"Variant with SKU '{request.Sku}' already exists for this product");

            // Create new variant
            var variant = new Domain.Entities.ProductVariant(
                request.Sku,
                request.Barcode,
                request.Cost
            );

            if (request.Weight.HasValue)
                variant.Weight = request.Weight;

            if (!string.IsNullOrWhiteSpace(request.Dimensions))
                variant.Dimensions = request.Dimensions;

            variant.ProductId = request.ProductId;

            // Add to product
            product.AddVariant(variant);

            // Save
            await _productRepository.UpdateAsync(product, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return new CreateProductVariantResponse
            {
                VariantId = variant.Id,
                Sku = variant.Sku,
                Barcode = variant.Barcode,
                Cost = variant.Cost
            };
        }
    }
}

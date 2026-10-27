using ERP.PRD.Application.Interfaces;
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
        public decimal? Length { get; set; }
        public decimal? Width { get; set; }
        public decimal? Height { get; set; }
        public string? DimensionUnit { get; set; }
        public string? WeightUnit { get; set; }
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

            RuleFor(x => x.Length)
                .GreaterThanOrEqualTo(0).When(x => x.Length.HasValue).WithMessage("Length must be positive");

            RuleFor(x => x.Width)
                .GreaterThanOrEqualTo(0).When(x => x.Width.HasValue).WithMessage("Width must be positive");

            RuleFor(x => x.Height)
                .GreaterThanOrEqualTo(0).When(x => x.Height.HasValue).WithMessage("Height must be positive");
        }
    }

    public class CreateProductVariantHandler : IRequestHandler<CreateProductVariantCommand, CreateProductVariantResponse>
    {
        private readonly IProductUnitOfWork _uow;

        public CreateProductVariantHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<CreateProductVariantResponse> Handle(CreateProductVariantCommand request, CancellationToken cancellationToken)
        {
            // Verify product exists and load variants
            var product = await _uow.Products.GetByIdWithVariantsAsync(request.ProductId, cancellationToken);

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
                variant.SetWeight(request.Weight.Value, request.WeightUnit ?? variant.WeightUnit ?? "kg");

            if (!string.IsNullOrWhiteSpace(request.Dimensions))
                variant.Dimensions = request.Dimensions;

            if (request.Length.HasValue || request.Width.HasValue || request.Height.HasValue)
                variant.SetDimensions(request.Length, request.Width, request.Height, request.DimensionUnit ?? variant.DimensionUnit ?? "cm");

            variant.ProductId = request.ProductId;

            // Add to product
            product.AddVariant(variant);

            // Explicit Add (the same lesson as the product images): the variant's Id is assigned before EF sees it, so the navigation alone leaves it Modified,
            // without its tenant, and the save is refused.
            await _uow.Variants.AddAsync(variant, cancellationToken);

            // Save
            await _uow.SaveChangesAsync(cancellationToken);

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

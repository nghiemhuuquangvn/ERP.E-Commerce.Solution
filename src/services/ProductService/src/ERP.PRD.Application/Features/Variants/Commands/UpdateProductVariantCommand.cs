using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Variants.Commands
{
    public class UpdateProductVariantCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
        public Guid VariantId { get; set; }
        public decimal? Cost { get; set; }
        public decimal? Weight { get; set; }
        public string? Barcode { get; set; }
        public string? Dimensions { get; set; }
        public decimal? Length { get; set; }
        public decimal? Width { get; set; }
        public decimal? Height { get; set; }
        public string? DimensionUnit { get; set; }
        public string? WeightUnit { get; set; }
    }

    public class UpdateProductVariantValidator : AbstractValidator<UpdateProductVariantCommand>
    {
        public UpdateProductVariantValidator()
        {
            RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product ID is required");
            RuleFor(x => x.VariantId).NotEmpty().WithMessage("Variant ID is required");

            RuleFor(x => x.Cost)
                .GreaterThanOrEqualTo(0).When(x => x.Cost.HasValue).WithMessage("Cost must be zero or positive")
                .LessThan(1000000).When(x => x.Cost.HasValue).WithMessage("Cost exceeds maximum");

            RuleFor(x => x.Weight)
                .GreaterThanOrEqualTo(0).When(x => x.Weight.HasValue).WithMessage("Weight must be positive");

            RuleFor(x => x.Barcode)
                .MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.Barcode)).WithMessage("Barcode too long");

            RuleFor(x => x.Length)
                .GreaterThanOrEqualTo(0).When(x => x.Length.HasValue).WithMessage("Length must be positive");

            RuleFor(x => x.Width)
                .GreaterThanOrEqualTo(0).When(x => x.Width.HasValue).WithMessage("Width must be positive");

            RuleFor(x => x.Height)
                .GreaterThanOrEqualTo(0).When(x => x.Height.HasValue).WithMessage("Height must be positive");
        }
    }

    public class UpdateProductVariantHandler : IRequestHandler<UpdateProductVariantCommand, Unit>
    {
        private readonly ProductDbContext _context;

        public UpdateProductVariantHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(UpdateProductVariantCommand request, CancellationToken cancellationToken)
        {
            var product = await _context.Products
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(x => x.Id == request.ProductId && !x.IsDeleted, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var variant = product.Variants.FirstOrDefault(v => v.Id == request.VariantId);
            if (variant == null)
                throw new KeyNotFoundException($"Variant with ID {request.VariantId} not found");

            // Update fields if provided
            if (request.Cost.HasValue)
                variant.Cost = request.Cost.Value;

            if (request.Weight.HasValue)
                variant.SetWeight(request.Weight.Value, request.WeightUnit ?? variant.WeightUnit ?? "kg");

            if (!string.IsNullOrWhiteSpace(request.Barcode))
                variant.Barcode = request.Barcode;

            if (!string.IsNullOrWhiteSpace(request.Dimensions))
                variant.Dimensions = request.Dimensions;

            if (request.Length.HasValue || request.Width.HasValue || request.Height.HasValue)
            {
                variant.SetDimensions(
                    request.Length ?? variant.Length,
                    request.Width ?? variant.Width,
                    request.Height ?? variant.Height,
                    request.DimensionUnit ?? variant.DimensionUnit ?? "cm");
            }

            variant.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

using MediatR;
using FluentValidation;

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
        }
    }

    public class UpdateProductVariantHandler : IRequestHandler<UpdateProductVariantCommand, Unit>
    {
        private readonly IRepository<Domain.Entities.Product> _productRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateProductVariantHandler(IRepository<Domain.Entities.Product> productRepository, IUnitOfWork unitOfWork)
        {
            _productRepository = productRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(UpdateProductVariantCommand request, CancellationToken cancellationToken)
        {
            var product = await _productRepository.GetAsync(
                x => x.Id == request.ProductId && !x.IsDeleted,
                cancellationToken
            );

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var variant = product.Variants.FirstOrDefault(v => v.Id == request.VariantId);
            if (variant == null)
                throw new KeyNotFoundException($"Variant with ID {request.VariantId} not found");

            // Update fields if provided
            if (request.Cost.HasValue)
                variant.Cost = request.Cost.Value;

            if (request.Weight.HasValue)
                variant.Weight = request.Weight.Value;

            if (!string.IsNullOrWhiteSpace(request.Barcode))
                variant.Barcode = request.Barcode;

            if (!string.IsNullOrWhiteSpace(request.Dimensions))
                variant.Dimensions = request.Dimensions;

            variant.UpdatedAt = DateTime.UtcNow;

            await _productRepository.UpdateAsync(product, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return Unit.Value;
        }
    }
}

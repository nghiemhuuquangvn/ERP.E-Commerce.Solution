using ERP.PRD.Domain.Entities;
using ERP.PRD.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Pricing.Commands
{
    public class SetProductPriceCommand : IRequest
    {
        public Guid VariantId { get; set; }
        public decimal BasePrice { get; set; }
        public decimal? DiscountedPrice { get; set; }
    }

    public class SetProductPriceValidator : AbstractValidator<SetProductPriceCommand>
    {
        public SetProductPriceValidator()
        {
            RuleFor(x => x.VariantId)
                .NotEqual(Guid.Empty).WithMessage("Variant ID is required");

            RuleFor(x => x.BasePrice)
                .GreaterThanOrEqualTo(0).WithMessage("Base price must be >= 0")
                .LessThan(1000000).WithMessage("Base price must be < 1,000,000");

            RuleFor(x => x.DiscountedPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Discounted price must be >= 0")
                .LessThan(1000000).WithMessage("Discounted price must be < 1,000,000")
                .When(x => x.DiscountedPrice.HasValue);

            RuleFor(x => x)
                .Custom((x, context) =>
                {
                    if (x.DiscountedPrice.HasValue && x.DiscountedPrice >= x.BasePrice)
                        context.AddFailure("DiscountedPrice", "Discounted price must be less than base price");
                });
        }
    }

    public class SetProductPriceHandler : IRequestHandler<SetProductPriceCommand>
    {
        private readonly ProductDbContext _context;
        private readonly ILogger<SetProductPriceHandler> _logger;

        public SetProductPriceHandler(ProductDbContext context, ILogger<SetProductPriceHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Handle(SetProductPriceCommand request, CancellationToken cancellationToken)
        {
            var variant = await _context.ProductVariants.FindAsync(new object[] { request.VariantId }, cancellationToken: cancellationToken);
            if (variant == null)
                throw new KeyNotFoundException($"Variant with ID {request.VariantId} not found");

            variant.Cost = request.BasePrice;
            variant.UpdatedAt = DateTime.UtcNow;
            variant.RequestSearchReindex();

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Price set for variant {VariantId}: Base={BasePrice}, Discounted={DiscountedPrice}",
                request.VariantId, request.BasePrice, request.DiscountedPrice ?? request.BasePrice);
        }
    }
}

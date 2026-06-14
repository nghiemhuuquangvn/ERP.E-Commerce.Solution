using ERP.PRD.Domain.Entities;
using ERP.PRD.Infrastructure.Persistence;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Pricing.Commands
{
    public class CreatePriceListCommand : IRequest<CreatePriceListResponse>
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public List<PriceListItemInput> Items { get; set; } = new();
    }

    public class PriceListItemInput
    {
        public Guid VariantId { get; set; }
        public decimal BasePrice { get; set; }
        public decimal? DiscountedPrice { get; set; }
    }

    public class CreatePriceListResponse
    {
        public Guid PriceListId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class CreatePriceListValidator : AbstractValidator<CreatePriceListCommand>
    {
        public CreatePriceListValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Price list name is required")
                .MaximumLength(100).WithMessage("Price list name cannot exceed 100 characters");

            RuleFor(x => x.EffectiveDate)
                .NotEmpty().WithMessage("Effective date is required");

            RuleFor(x => x.Items)
                .NotEmpty().WithMessage("At least one price item is required");

            RuleForEach(x => x.Items)
                .SetValidator(new PriceListItemValidator());
        }
    }

    public class PriceListItemValidator : AbstractValidator<PriceListItemInput>
    {
        public PriceListItemValidator()
        {
            RuleFor(x => x.VariantId)
                .NotEqual(Guid.Empty).WithMessage("Variant ID is required");

            RuleFor(x => x.BasePrice)
                .GreaterThanOrEqualTo(0).WithMessage("Base price must be >= 0")
                .LessThan(1000000).WithMessage("Base price must be < 1,000,000");

            RuleFor(x => x)
                .Custom((x, context) =>
                {
                    if (x.DiscountedPrice.HasValue && x.DiscountedPrice >= x.BasePrice)
                        context.AddFailure("DiscountedPrice", "Discounted price must be less than base price");
                });
        }
    }

    public class CreatePriceListHandler : IRequestHandler<CreatePriceListCommand, CreatePriceListResponse>
    {
        private readonly ProductDbContext _context;
        private readonly ILogger<CreatePriceListHandler> _logger;

        public CreatePriceListHandler(ProductDbContext context, ILogger<CreatePriceListHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CreatePriceListResponse> Handle(CreatePriceListCommand request, CancellationToken cancellationToken)
        {
            var priceList = PriceList.Create(request.Name, request.EffectiveDate, isActive: true);
            priceList.Description = request.Description;

            if (request.ExpiryDate.HasValue)
                priceList.SetExpiry(request.ExpiryDate.Value);

            foreach (var item in request.Items)
            {
                var variant = await _context.ProductVariants.FindAsync(new object[] { item.VariantId }, cancellationToken: cancellationToken);
                if (variant == null)
                    throw new KeyNotFoundException($"Variant with ID {item.VariantId} not found");

                priceList.AddItem(item.VariantId, item.BasePrice, item.DiscountedPrice);
            }

            _context.PriceLists.Add(priceList);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Price list created: {PriceListId}, Name: {Name}, Items: {Count}",
                priceList.Id, priceList.Name, request.Items.Count);

            return new CreatePriceListResponse
            {
                PriceListId = priceList.Id,
                Name = priceList.Name
            };
        }
    }
}

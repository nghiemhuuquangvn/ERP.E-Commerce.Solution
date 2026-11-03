using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Pricing.Commands
{
    /// <summary>
    /// Sets the RETAIL price of a variant: one row (quantity tier 1) in the shop's "Default retail" price list, created on first use. It used to
    /// overwrite the variant's Cost, so no client ever saw a selling price (DEFERRED R111); cost is edited through the variant commands.
    /// </summary>
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
        private readonly IProductUnitOfWork _uow;
        private readonly PriceChangeTracker _changes;
        private readonly ILogger<SetProductPriceHandler> _logger;

        public SetProductPriceHandler(IProductUnitOfWork uow, PriceChangeTracker changes, ILogger<SetProductPriceHandler> logger)
        {
            _uow = uow;
            _changes = changes;
            _logger = logger;
        }

        public async Task Handle(SetProductPriceCommand request, CancellationToken cancellationToken)
        {
            var variant = await _uow.Variants.GetByIdAsync(request.VariantId, cancellationToken);
            if (variant == null)
                throw new KeyNotFoundException($"Variant with ID {request.VariantId} not found");

            var before = await _changes.SnapshotAsync(new[] { variant.ProductId }, cancellationToken);

            await _uow.BeginTransactionAsync(cancellationToken);
            try
            {
                var list = await _uow.PriceLists.GetDefaultRetailListAsync(cancellationToken);
                var isNewList = list == null;
                list ??= PriceList.Create(PriceList.DefaultRetailName, DateTime.UtcNow, isActive: true, channel: PriceListChannel.Retail);

                var item = list.Items.FirstOrDefault(i => i.VariantId == request.VariantId && i.MinQuantity == 1);
                if (item != null)
                {
                    list.UpdatePrice(request.VariantId, request.BasePrice, request.DiscountedPrice);
                }
                else
                {
                    list.AddItem(request.VariantId, request.BasePrice, request.DiscountedPrice);
                    if (isNewList)
                        await _uow.PriceLists.AddAsync(list, cancellationToken);   // the whole new graph is added together
                    else
                        _uow.PriceLists.MarkItemAdded(list.Items.First(i => i.VariantId == request.VariantId && i.MinQuantity == 1));
                }

                variant.UpdatedAt = DateTime.UtcNow;
                variant.RequestSearchReindex();
                await _uow.SaveChangesAsync(cancellationToken);

                var raised = await _changes.PublishChangesAsync(before, cancellationToken);
                await _uow.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Retail price set for variant {VariantId}: Base={BasePrice}, Discounted={DiscountedPrice}, price event raised: {Raised}",
                    request.VariantId, request.BasePrice, request.DiscountedPrice ?? request.BasePrice, raised > 0);
            }
            catch
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}

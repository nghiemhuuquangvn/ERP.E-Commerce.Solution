using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Enums;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Pricing.Commands
{
    public class SetPriceListChannelCommand : IRequest<Unit>
    {
        public Guid PriceListId { get; set; }
        public PriceListChannel Channel { get; set; }

        /// <summary>
        /// References SharedMasterDataService's Currency by Id only (cross-service,
        /// no database FK — see CLAUDE.md database ownership rules). Null means the
        /// tenant's default currency applies.
        /// </summary>
        public Guid? CurrencyId { get; set; }
    }

    public class SetPriceListChannelValidator : AbstractValidator<SetPriceListChannelCommand>
    {
        public SetPriceListChannelValidator()
        {
            RuleFor(x => x.PriceListId)
                .NotEqual(Guid.Empty).WithMessage("Price list ID is required");

            RuleFor(x => x.Channel)
                .IsInEnum().WithMessage("Invalid channel");
        }
    }

    public class SetPriceListChannelHandler : IRequestHandler<SetPriceListChannelCommand, Unit>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly PriceChangeTracker _changes;

        public SetPriceListChannelHandler(IProductUnitOfWork uow, PriceChangeTracker changes)
        {
            _uow = uow;
            _changes = changes;
        }

        public async Task<Unit> Handle(SetPriceListChannelCommand request, CancellationToken cancellationToken)
        {
            var priceList = await _uow.PriceLists.GetByIdWithItemsAsync(request.PriceListId, cancellationToken);

            if (priceList == null)
                throw new KeyNotFoundException($"Price list with ID {request.PriceListId} not found");

            // Moving a list to or from the retail channels changes what its products sell for: tell POS / Sales.
            var productIds = await _uow.PriceLists.GetProductIdsOfVariantsAsync(priceList.Items.Select(i => i.VariantId), cancellationToken);
            var before = await _changes.SnapshotAsync(productIds, cancellationToken);

            await _uow.BeginTransactionAsync(cancellationToken);
            try
            {
                priceList.SetChannelAndCurrency(request.Channel, request.CurrencyId);
                await _uow.SaveChangesAsync(cancellationToken);
                await _changes.PublishChangesAsync(before, cancellationToken);
                await _uow.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                throw;
            }

            return Unit.Value;
        }
    }
}

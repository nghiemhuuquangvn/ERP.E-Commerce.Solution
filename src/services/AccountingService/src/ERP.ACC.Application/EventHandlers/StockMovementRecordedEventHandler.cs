using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.EventHandlers
{
    /// <summary>
    /// WarehouseService's <c>StockMovementRecordedEvent</c> (RabbitMQ <c>warehouse_exchange</c>), reduced to what the ledger needs.
    /// <see cref="ValueDelta"/> is the signed change of the stock value computed by Warehouse; the quantity has no consistent sign.
    /// </summary>
    public class StockMovementRecordedIntegrationEvent : INotification
    {
        public Guid MovementId { get; set; }
        public string MovementType { get; set; } = string.Empty;
        public decimal ValueDelta { get; set; }
        public string? Reference { get; set; }
        public DateTime RecordedAt { get; set; }
    }

    /// <summary>
    /// Books a stock movement: receipt Dr Inventory / Cr GRNI, issue Dr COGS / Cr Inventory, adjustment against the
    /// InventoryAdjustment key (docs/business/Accounting/AutoPosting.md). Idempotent per movement (the journal entry's SourceKey).
    /// </summary>
    public class StockMovementRecordedIntegrationEventHandler : INotificationHandler<StockMovementRecordedIntegrationEvent>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<StockMovementRecordedIntegrationEventHandler> _logger;

        public StockMovementRecordedIntegrationEventHandler(
            IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<StockMovementRecordedIntegrationEventHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task Handle(StockMovementRecordedIntegrationEvent @event, CancellationToken cancellationToken)
        {
            var date = @event.RecordedAt == default ? DateTime.UtcNow : @event.RecordedAt;
            var request = PostingRules.ForStockMovement(@event.MovementId, @event.MovementType, @event.ValueDelta, date, @event.Reference);
            if (request == null)
            {
                _logger.LogDebug("Stock movement {MovementId} ({Type}) changes no stock value, nothing to post", @event.MovementId, @event.MovementType);
                return;
            }

            await _posting.TryPostAsync(_logger, request, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
        }
    }
}

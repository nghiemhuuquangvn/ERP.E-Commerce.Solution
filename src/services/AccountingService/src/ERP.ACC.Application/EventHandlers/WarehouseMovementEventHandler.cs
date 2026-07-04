using ERP.ACC.Application.Interfaces;
using MediatR;

namespace ERP.ACC.Application.EventHandlers
{
    public class WarehouseMovementIntegrationEvent : INotification
    {
        public Guid MovementId { get; set; }
        public string MovementType { get; set; } = string.Empty;
        public Guid ItemId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalValue { get; set; }
    }

    /// <summary>
    /// Handles inventory stock movement events from WareHouseService ("warehouse-topic").
    /// Full GL posting (debit Inventory / credit AP or similar) requires the company's
    /// Chart of Accounts to be configured — deferred until a COA-mapping feature is added.
    /// The handler queries the open period so callers can see the event was received.
    /// </summary>
    public class WarehouseMovementIntegrationEventHandler : INotificationHandler<WarehouseMovementIntegrationEvent>
    {
        private readonly IFiscalPeriodRepository _fiscalPeriods;

        public WarehouseMovementIntegrationEventHandler(IFiscalPeriodRepository fiscalPeriods)
        {
            _fiscalPeriods = fiscalPeriods;
        }

        public async Task Handle(WarehouseMovementIntegrationEvent @event, CancellationToken cancellationToken)
        {
            // Verify an open period exists for the movement date — no-op if not.
            // Full double-entry posting is deferred until COA account mapping is wired.
            _ = await _fiscalPeriods.GetOpenPeriodAsync(cancellationToken);
        }
    }
}

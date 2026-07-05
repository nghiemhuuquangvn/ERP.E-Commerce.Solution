using ERP.SHP.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.SHP.Application.EventHandlers;

/// <summary>
/// Integration event consumed from WarehouseService's "warehouse_exchange" (RabbitMQ).
/// This was previously a log-only no-op — the "schedule pickup" business meaning of
/// InventoryReserved never actually touched a Shipment. A Shipment for the order may
/// not exist yet at this point (it's created separately, in reaction to
/// SalesOrderApprovedEvent, which can arrive before or after this one): if it doesn't
/// exist yet, there's genuinely nothing to schedule a pickup for, so this still just
/// logs in that case; if it does exist and is still Pending, it's transitioned to
/// ReadyForPickup so the carrier pickup step has a real, queryable trigger.
/// </summary>
public class InventoryReservedIntegrationEvent : INotification
{
    public Guid ReservationId { get; set; }
    public Guid OrderId { get; set; }
}

public class InventoryReservedEventHandler : INotificationHandler<InventoryReservedIntegrationEvent>
{
    private readonly IShippingUnitOfWork _uow;
    private readonly ILogger<InventoryReservedEventHandler> _logger;

    public InventoryReservedEventHandler(IShippingUnitOfWork uow, ILogger<InventoryReservedEventHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task Handle(InventoryReservedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        var shipments = await _uow.Shipments.GetTrackedByOrderIdAsync(@event.OrderId, cancellationToken);
        if (shipments.Count == 0)
        {
            _logger.LogInformation(
                "Inventory reserved for order {OrderId} (reservation {ReservationId}) but no Shipment exists yet — nothing to schedule.",
                @event.OrderId, @event.ReservationId);
            return;
        }

        foreach (var shipment in shipments)
        {
            shipment.MarkReadyForPickup();
            await _uow.Shipments.UpdateAsync(shipment, cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }
}

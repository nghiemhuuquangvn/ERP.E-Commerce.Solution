using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.SHP.Application.EventHandlers;

/// <summary>
/// Integration event consumed from WarehouseService's "warehouse_exchange" (RabbitMQ).
/// This is the previously-missing "InventoryReserved event consumer" gap. A Shipment
/// for the order may not exist yet at this point (it's created separately, in
/// reaction to SalesOrderApprovedEvent, which can arrive before or after this one) —
/// so this only logs receipt for correlation rather than inventing new Shipment
/// state-machine behavior; wiring inventory-confirmed status onto Shipment is a
/// follow-up once both events' arrival order is handled deliberately.
/// </summary>
public class InventoryReservedIntegrationEvent : INotification
{
    public Guid ReservationId { get; set; }
    public Guid OrderId { get; set; }
}

public class InventoryReservedEventHandler : INotificationHandler<InventoryReservedIntegrationEvent>
{
    private readonly ILogger<InventoryReservedEventHandler> _logger;

    public InventoryReservedEventHandler(ILogger<InventoryReservedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(InventoryReservedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Inventory reserved for order {OrderId} (reservation {ReservationId}) — ready to ship once ready.",
            @event.OrderId, @event.ReservationId);

        return Task.CompletedTask;
    }
}

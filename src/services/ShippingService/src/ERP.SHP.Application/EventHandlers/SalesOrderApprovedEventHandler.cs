using ERP.SHP.Application.Commands;
using MediatR;

namespace ERP.SHP.Application.EventHandlers;

// SaleOrderService's real SalesOrderApprovedEvent only carries (OrderId, CustomerId,
// ApprovedAt, TotalAmount) — no ShippingAddress, no line items. Those two fields are
// therefore placeholders below rather than guessed data; a real address/item sync
// (e.g. SaleOrderService enriching this event, or Shipping querying its own local
// cache built from a richer SalesOrderCreated payload) is a follow-up, not solvable
// here without a synchronous cross-service call (forbidden by this repo's
// architecture rules).
public class SalesOrderApprovedIntegrationEvent : INotification
    {
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
}

public class SalesOrderApprovedIntegrationEventHandler : INotificationHandler<SalesOrderApprovedIntegrationEvent>
{
    private readonly IMediator _mediator;
    public SalesOrderApprovedIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(SalesOrderApprovedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        // Use default carrier — in production, carrier selection logic goes here
        var defaultCarrierId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        await _mediator.Send(new CreateShipmentCommand
        {
            OrderId = @event.OrderId,
            CarrierId = defaultCarrierId,
            ShippingAddress = "(pending address sync)",
            EstimatedDelivery = DateTime.UtcNow.AddDays(5),
            Items = new List<ShipmentItemRequest>()
        }, cancellationToken);
    }
}


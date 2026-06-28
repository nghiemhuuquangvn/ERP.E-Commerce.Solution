using ERP.SHP.Application.Commands;
using MediatR;

namespace ERP.SHP.Application.EventHandlers;

public class SalesOrderApprovedIntegrationEvent
{
    public Guid OrderId { get; set; }
    public string ShippingAddress { get; set; } = string.Empty;
    public List<OrderLineItem> LineItems { get; set; } = new();
}

public class OrderLineItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
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
            ShippingAddress = @event.ShippingAddress,
            EstimatedDelivery = DateTime.UtcNow.AddDays(5),
            Items = @event.LineItems.Select(li => new ShipmentItemRequest
            {
                ProductId = li.ProductId,
                ProductName = li.ProductName,
                Quantity = li.Quantity,
                Weight = 0.5m
            }).ToList()
        }, cancellationToken);
    }
}

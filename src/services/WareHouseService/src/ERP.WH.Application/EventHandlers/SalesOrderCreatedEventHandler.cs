using MediatR;
using ERP.WH.Application.Commands.Inventory;

namespace ERP.WH.Application.EventHandlers;

public class SalesOrderCreatedEvent : INotification
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderLineItem> LineItems { get; set; }
    public string OrderStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CorrelationId { get; set; }
}

public class OrderLineItem
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class SalesOrderCreatedEventHandler : INotificationHandler<SalesOrderCreatedEvent>
{
    private readonly IMediator _mediator;

    public SalesOrderCreatedEventHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Handle(SalesOrderCreatedEvent @event, CancellationToken cancellationToken)
    {
        var holdUntil = DateTime.UtcNow.AddDays(7);

        var items = @event.LineItems.Select(li => new ReservationItemDto
        {
            ProductId = li.ProductId,
            Quantity = li.Quantity
        }).ToList();

        var command = new ReserveInventoryCommand
        {
            OrderId = @event.OrderId,
            Items = items,
            HoldUntil = holdUntil,
            CorrelationId = @event.CorrelationId
        };

        await _mediator.Send(command, cancellationToken);
    }
}

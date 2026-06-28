using ERP.PAY.Application.Commands;
using ERP.PAY.Domain.Enums;
using MediatR;

namespace ERP.PAY.Application.EventHandlers;

public class SalesOrderCreatedIntegrationEvent
{
    public Guid OrderId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string PaymentMethod { get; set; } = "CreditCard";
}

public class SalesOrderCreatedIntegrationEventHandler : INotificationHandler<SalesOrderCreatedIntegrationEvent>
{
    private readonly IMediator _mediator;
    public SalesOrderCreatedIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(SalesOrderCreatedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PaymentMethod>(@event.PaymentMethod, out var method))
            method = PaymentMethod.CreditCard;

        await _mediator.Send(new InitiatePaymentCommand
        {
            OrderId = @event.OrderId,
            Amount = @event.TotalAmount,
            Currency = @event.Currency,
            Method = method
        }, cancellationToken);
    }
}

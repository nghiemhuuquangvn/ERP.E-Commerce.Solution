using ERP.ACC.Application.Commands;
using MediatR;

namespace ERP.ACC.Application.EventHandlers
{
    public class SalesOrderCreatedIntegrationEvent : INotification
    {
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class SalesOrderCreatedIntegrationEventHandler : INotificationHandler<SalesOrderCreatedIntegrationEvent>
    {
        private readonly IMediator _mediator;

        public SalesOrderCreatedIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

        public async Task Handle(SalesOrderCreatedIntegrationEvent @event, CancellationToken cancellationToken)
        {
            var invoiceNumber = $"INV-{@event.OrderId:N}".Substring(0, 20).ToUpper();

            await _mediator.Send(new CreateInvoiceCommand
            {
                InvoiceNumber = invoiceNumber,
                CustomerId = @event.CustomerId,
                OrderId = @event.OrderId,
                Amount = @event.TotalAmount,
                DueDate = DateTime.UtcNow.AddDays(30)
            }, cancellationToken);
        }
    }
}


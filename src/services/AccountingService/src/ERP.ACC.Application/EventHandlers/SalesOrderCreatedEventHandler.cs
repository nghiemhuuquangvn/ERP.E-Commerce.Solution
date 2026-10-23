using ERP.ACC.Application.Commands;
using ERP.ACC.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

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
        private readonly IInvoiceRepository _invoices;
        private readonly ILogger<SalesOrderCreatedIntegrationEventHandler> _logger;

        public SalesOrderCreatedIntegrationEventHandler(
            IMediator mediator, IInvoiceRepository invoices, ILogger<SalesOrderCreatedIntegrationEventHandler> logger)
        {
            _mediator = mediator;
            _invoices = invoices;
            _logger = logger;
        }

        public async Task Handle(SalesOrderCreatedIntegrationEvent @event, CancellationToken cancellationToken)
        {
            // One invoice per order: a redelivered event used to hit the invoice number's unique index and be requeued for ever.
            if (await _invoices.GetByOrderIdAsync(@event.OrderId, cancellationToken) != null)
            {
                _logger.LogInformation("Order {OrderId} already has an invoice; redelivered event ignored", @event.OrderId);
                return;
            }

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

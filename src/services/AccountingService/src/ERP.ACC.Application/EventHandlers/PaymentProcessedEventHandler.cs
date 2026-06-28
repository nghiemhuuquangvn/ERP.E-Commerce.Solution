using ERP.ACC.Application.Commands;
using ERP.ACC.Application.Interfaces;
using MediatR;

namespace ERP.ACC.Application.EventHandlers
{
    public class PaymentProcessedIntegrationEvent
    {
        public Guid PaymentId { get; set; }
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
    }

    public class PaymentProcessedIntegrationEventHandler : INotificationHandler<PaymentProcessedIntegrationEvent>
    {
        private readonly IMediator _mediator;
        private readonly IInvoiceRepository _invoices;

        public PaymentProcessedIntegrationEventHandler(IMediator mediator, IInvoiceRepository invoices)
        {
            _mediator = mediator;
            _invoices = invoices;
        }

        public async Task Handle(PaymentProcessedIntegrationEvent @event, CancellationToken cancellationToken)
        {
            var invoices = await _invoices.GetAllAsync(cancellationToken);
            var invoice = invoices.FirstOrDefault(i => i.OrderId == @event.OrderId && i.Status == 0);

            if (invoice == null)
                return;

            await _mediator.Send(new MarkInvoicePaidCommand
            {
                InvoiceId = invoice.Id
            }, cancellationToken);
        }
    }
}

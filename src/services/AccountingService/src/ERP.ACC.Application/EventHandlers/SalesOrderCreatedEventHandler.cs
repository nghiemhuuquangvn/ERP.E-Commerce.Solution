using ERP.ACC.Application.Commands;
using ERP.ACC.Application.EInvoicing;
using ERP.Shared.Infrastructure.Data;
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
        /// <summary>The delivery fee inside TotalAmount (ROADMAP P1-11b); 0 on events published before it.</summary>
        public decimal ShippingFee { get; set; }
    }

    public class SalesOrderCreatedIntegrationEventHandler : INotificationHandler<SalesOrderCreatedIntegrationEvent>
    {
        private readonly IMediator _mediator;
        private readonly IInvoiceRepository _invoices;
        private readonly ILogger<SalesOrderCreatedIntegrationEventHandler> _logger;

        private readonly ITaxSettingsService? _settings;
        private readonly ITenantProvider? _tenant;

        public SalesOrderCreatedIntegrationEventHandler(
            IMediator mediator, IInvoiceRepository invoices, ILogger<SalesOrderCreatedIntegrationEventHandler> logger,
            ITaxSettingsService? settings = null, ITenantProvider? tenant = null)
        {
            _mediator = mediator;
            _invoices = invoices;
            _logger = logger;
            _settings = settings;
            _tenant = tenant;
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

            var created = await _mediator.Send(new CreateInvoiceCommand
            {
                InvoiceNumber = invoiceNumber,
                CustomerId = @event.CustomerId,
                OrderId = @event.OrderId,
                Amount = @event.TotalAmount,
                ShippingAmount = @event.ShippingFee,
                DueDate = DateTime.UtcNow.AddDays(30)
            }, cancellationToken);

            // the tenant asked for an e-invoice with every invoice (ROADMAP P1-3b): a retail customer, errors are logged and the invoice is kept
            if (_settings != null && _tenant?.CurrentTenantId is { } tenantId && (await _settings.GetTenantSettingsAsync(tenantId, cancellationToken)).AutoIssueEInvoice)
            {
                try
                {
                    await _mediator.Send(new IssueEInvoiceCommand { InvoiceId = created.Id }, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Invoice {Invoice} was created but its e-invoice was not issued automatically", invoiceNumber);
                }
            }
        }
    }
}

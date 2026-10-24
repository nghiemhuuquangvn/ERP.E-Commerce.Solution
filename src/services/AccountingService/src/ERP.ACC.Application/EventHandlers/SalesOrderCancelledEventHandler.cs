using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.EventHandlers
{
    public class SalesOrderCancelledIntegrationEvent : INotification
    {
        public Guid OrderId { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>
    /// A cancelled order takes its invoice back: the invoice is cancelled and the revenue entry reversed. When the invoice was already
    /// paid, the cash stays booked and the customer's refund arrives as a Payment refund entry (ROADMAP P1-1e).
    /// </summary>
    public class SalesOrderCancelledIntegrationEventHandler : INotificationHandler<SalesOrderCancelledIntegrationEvent>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<SalesOrderCancelledIntegrationEventHandler> _logger;

        public SalesOrderCancelledIntegrationEventHandler(
            IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<SalesOrderCancelledIntegrationEventHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task Handle(SalesOrderCancelledIntegrationEvent @event, CancellationToken cancellationToken)
        {
            var invoice = await _uow.Invoices.GetByOrderIdAsync(@event.OrderId, cancellationToken);
            if (invoice == null)
            {
                _logger.LogInformation("Order {OrderId} was cancelled and has no invoice", @event.OrderId);
                return;
            }

            if (invoice.Status == (int)InvoiceStatus.Cancelled)
                return; // redelivered

            if (invoice.AmountPaid > 0)
                _logger.LogWarning("Order {OrderId} was cancelled after {Paid} of invoice {Invoice} was paid; the revenue is reversed and the customer's money stays as a credit until the payment refund is booked",
                    @event.OrderId, invoice.AmountPaid, invoice.InvoiceNumber);

            invoice.Cancel();
            await _uow.Invoices.UpdateAsync(invoice, cancellationToken);
            await _posting.TryReverseAsync(_logger, PostingRules.Invoice, invoice.Id,
                string.IsNullOrWhiteSpace(@event.Reason) ? "Order cancelled" : $"Order cancelled: {@event.Reason}",
                DateTime.UtcNow, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
        }
    }
}

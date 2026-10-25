using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.EventHandlers
{
    /// <summary>
    /// PaymentService's <c>PaymentRefundedEvent</c> (Kafka <c>payment-topic</c>), published only for a refund the gateway confirmed
    /// (ROADMAP P1-1e). <see cref="RefundId"/> is empty on events published before that change, which were raised before the gateway
    /// answered and must not be booked.
    /// </summary>
    public class PaymentRefundedIntegrationEvent : INotification
    {
        public Guid RefundId { get; set; }
        public Guid PaymentId { get; set; }
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }

        /// <summary>PaymentService's PaymentMethod value (Cash = 4); decides cash or bank.</summary>
        public int Method { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }

    /// <summary>SaleOrderService's <c>SalesOrderReturnedEvent</c> (RabbitMQ): a return was completed and this amount is owed to the customer.</summary>
    public class SalesOrderReturnedIntegrationEvent : INotification
    {
        public Guid OrderId { get; set; }
        public decimal RefundAmount { get; set; }
        public string? Reason { get; set; }
        public DateTime ReturnedAt { get; set; }
    }

    /// <summary>
    /// Money returned to the customer: Dr Accounts receivable / Cr Cash or Bank, the reverse of the payment's entry, and the invoice's paid
    /// amount goes down. The entry is the idempotency record (a redelivered event does nothing).
    /// </summary>
    public class PaymentRefundedIntegrationEventHandler : INotificationHandler<PaymentRefundedIntegrationEvent>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<PaymentRefundedIntegrationEventHandler> _logger;

        public PaymentRefundedIntegrationEventHandler(
            IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<PaymentRefundedIntegrationEventHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task Handle(PaymentRefundedIntegrationEvent @event, CancellationToken cancellationToken)
        {
            if (@event.RefundId == Guid.Empty || @event.Amount <= 0)
            {
                _logger.LogInformation("Payment {PaymentId} refund event has no confirmed refund id (published before the gateway answered); nothing posted", @event.PaymentId);
                return;
            }

            var sourceKey = JournalEntry.BuildSourceKey(PostingRules.PaymentRefund, @event.RefundId);
            if (await _uow.JournalEntries.GetBySourceKeyAsync(sourceKey, cancellationToken) != null)
            {
                _logger.LogInformation("Refund {RefundId} is already in the ledger; redelivered event ignored", @event.RefundId);
                return;
            }

            var invoice = await _uow.Invoices.GetByOrderIdAsync(@event.OrderId, cancellationToken);
            if (invoice != null)
            {
                invoice.ApplyRefund(@event.Amount);
                await _uow.Invoices.UpdateAsync(invoice, cancellationToken);
                await _uow.PaymentApplications.AddAsync(PaymentApplication.Create(
                    PaymentApplication.InvoiceDocument, invoice.Id, @event.ProcessedAt ?? DateTime.UtcNow, -@event.Amount, $"refund:{@event.RefundId:N}"), cancellationToken);
            }
            else
            {
                _logger.LogWarning("Refund {RefundId} for order {OrderId} has no invoice; the cash is booked against receivables anyway", @event.RefundId, @event.OrderId);
            }

            await _posting.TryPostAsync(_logger,
                PostingRules.ForPaymentRefund(@event.RefundId, @event.OrderId, @event.Amount, @event.Method, @event.ProcessedAt ?? DateTime.UtcNow),
                cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// A completed return: Dr Sales returns / Cr Accounts receivable for the refund amount (a credit note against the order's invoice). The
    /// cash leaves with the Payment refund, which has its own entry. The goods are not put back into stock or costed back here (ROADMAP P1-5).
    /// </summary>
    public class SalesOrderReturnedIntegrationEventHandler : INotificationHandler<SalesOrderReturnedIntegrationEvent>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<SalesOrderReturnedIntegrationEventHandler> _logger;

        public SalesOrderReturnedIntegrationEventHandler(
            IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<SalesOrderReturnedIntegrationEventHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task Handle(SalesOrderReturnedIntegrationEvent @event, CancellationToken cancellationToken)
        {
            var invoice = await _uow.Invoices.GetByOrderIdAsync(@event.OrderId, cancellationToken);
            if (invoice == null)
            {
                _logger.LogWarning("Order {OrderId} was returned but has no invoice; no credit was posted", @event.OrderId);
                return;
            }

            if (@event.RefundAmount <= 0m || @event.RefundAmount > invoice.Amount)
            {
                _logger.LogError("Return of order {OrderId}: refund amount {Refund} is not within the invoice amount {Amount}; no credit was posted",
                    @event.OrderId, @event.RefundAmount, invoice.Amount);
                return;
            }

            await _posting.TryPostAsync(_logger,
                PostingRules.ForSalesReturn(@event.OrderId, invoice.InvoiceNumber, @event.RefundAmount,
                    @event.ReturnedAt == default ? DateTime.UtcNow : @event.ReturnedAt, invoice.TaxRatePercent),
                cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
        }
    }
}

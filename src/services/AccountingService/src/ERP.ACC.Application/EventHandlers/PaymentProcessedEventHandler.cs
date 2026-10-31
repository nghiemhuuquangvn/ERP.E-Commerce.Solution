using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.EventHandlers
{
    public class PaymentProcessedIntegrationEvent : INotification
    {
        public Guid PaymentId { get; set; }
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }

        /// <summary>PaymentService's PaymentMethod value (Cash = 4); decides cash or bank.</summary>
        public int Method { get; set; }
        public DateTime? ProcessedAt { get; set; }

        /// <summary>One part of a payment (the parcel's shipment id for cash on delivery, ROADMAP R43). Null = the payment was collected in one go.</summary>
        public Guid? CaptureId { get; set; }
    }

    /// <summary>
    /// A captured payment: the money is booked (Dr Cash / Bank, Cr Accounts receivable) and applied to the order's invoice.
    /// The entry is the idempotency record: a redelivered event finds it and does nothing, so the invoice is not credited twice.
    /// Cash on delivery of an order in several parcels sends one event per parcel (R43): each part has its own entry and its own credit to the invoice.
    /// </summary>
    public class PaymentProcessedIntegrationEventHandler : INotificationHandler<PaymentProcessedIntegrationEvent>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<PaymentProcessedIntegrationEventHandler> _logger;

        public PaymentProcessedIntegrationEventHandler(
            IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<PaymentProcessedIntegrationEventHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task Handle(PaymentProcessedIntegrationEvent @event, CancellationToken cancellationToken)
        {
            if (@event.Amount <= 0)
                return;

            // The idempotency key is the capture when there is one (one per parcel), else the payment.
            var sourceId = @event.CaptureId ?? @event.PaymentId;
            var sourceKey = Domain.Entities.JournalEntry.BuildSourceKey(PostingRules.Payment, sourceId);
            if (await _uow.JournalEntries.GetBySourceKeyAsync(sourceKey, cancellationToken) != null)
            {
                _logger.LogInformation("Payment {PaymentId} is already in the ledger; redelivered event ignored", @event.PaymentId);
                return;
            }

            // The ledger entry is the idempotency record, but when no period was open there is none (risk R13): the credit to the
            // invoice must not be repeated on redelivery, yet the posting is tried again (the period may be open by then).
            var applicationKey = $"payment:{sourceId:N}";
            var alreadyApplied = await _uow.PaymentApplications.ExistsBySourceKeyAsync(applicationKey, cancellationToken);

            var invoice = alreadyApplied ? null : await _uow.Invoices.GetByOrderIdAsync(@event.OrderId, cancellationToken);
            if (invoice != null && invoice.ApplyPayment(@event.Amount))
            {
                await _uow.Invoices.UpdateAsync(invoice, cancellationToken);
                await _uow.PaymentApplications.AddAsync(Domain.Entities.PaymentApplication.Create(
                    Domain.Entities.PaymentApplication.InvoiceDocument, invoice.Id, @event.ProcessedAt ?? DateTime.UtcNow, @event.Amount, applicationKey), cancellationToken);
            }
            else if (alreadyApplied)
                _logger.LogInformation("Payment {PaymentId} was already applied to its invoice; only the ledger posting is retried", @event.PaymentId);
            else if (invoice == null)
                _logger.LogWarning("Payment {PaymentId} for order {OrderId} has no invoice yet; the cash is booked against receivables anyway",
                    @event.PaymentId, @event.OrderId);

            // The cash really arrived whatever the invoice looks like, so it is booked regardless.
            await _posting.TryPostAsync(_logger,
                PostingRules.ForPayment(sourceId, @event.OrderId, @event.Amount, @event.Method, @event.ProcessedAt ?? DateTime.UtcNow),
                cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
        }
    }
}

using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.EventHandlers
{
    public sealed record PosTenderLine(string Method, decimal Amount);

    /// <summary>
    /// POSService's <c>TransactionCreatedEvent</c> (Kafka <c>pos-topic</c>) reduced to the money (ROADMAP P1-1c). <see cref="HasAmounts"/> is
    /// false for events published before the amounts were added; those cannot be posted and are skipped.
    /// </summary>
    public class PosSaleCompletedIntegrationEvent : INotification
    {
        public Guid TransactionId { get; set; }
        public bool HasAmounts { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public IReadOnlyList<PosTenderLine> Tenders { get; set; } = Array.Empty<PosTenderLine>();
        public DateTime CompletedAt { get; set; }

        /// <summary>Set for click-and-collect sales whose money the order / payment flow has already booked.</summary>
        public Guid? ExternalPaymentId { get; set; }
    }

    public class PosSaleVoidedIntegrationEvent : INotification
    {
        public Guid TransactionId { get; set; }
        public string? Reason { get; set; }
        public DateTime VoidedAt { get; set; }
    }

    public class PosShiftClosedIntegrationEvent : INotification
    {
        public Guid ShiftId { get; set; }

        /// <summary>Counted minus expected cash; null on events published before the variance was added.</summary>
        public decimal? CashVariance { get; set; }
        public DateTime ClosedAt { get; set; }
    }

    /// <summary>
    /// Revenue and cash of a POS sale: Dr Cash / Bank per tender, Cr Sales revenue (net of the discount), Cr VAT. The cost of goods is
    /// NOT posted here: Warehouse's stock movement already books it (Dr COGS / Cr Inventory, P1-1b).
    /// </summary>
    public class PosSaleCompletedIntegrationEventHandler : INotificationHandler<PosSaleCompletedIntegrationEvent>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<PosSaleCompletedIntegrationEventHandler> _logger;

        public PosSaleCompletedIntegrationEventHandler(
            IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<PosSaleCompletedIntegrationEventHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task Handle(PosSaleCompletedIntegrationEvent @event, CancellationToken cancellationToken)
        {
            if (@event.ExternalPaymentId.HasValue)
            {
                _logger.LogInformation("POS sale {TransactionId} was paid online ({PaymentId}); its money is booked by the payment flow, nothing posted",
                    @event.TransactionId, @event.ExternalPaymentId);
                return;
            }

            if (!@event.HasAmounts)
            {
                _logger.LogInformation("POS sale {TransactionId} predates the amounts on the event; nothing posted", @event.TransactionId);
                return;
            }

            var request = PostingRules.ForPosSale(@event.TransactionId, @event.SubTotal, @event.DiscountAmount, @event.TaxAmount,
                @event.Tenders, @event.CompletedAt == default ? DateTime.UtcNow : @event.CompletedAt);
            if (request == null)
            {
                _logger.LogError("POS sale {TransactionId} has tenders that do not add up to subtotal - discount + tax; no journal entry was posted",
                    @event.TransactionId);
                return;
            }

            await _posting.TryPostAsync(_logger, request, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>A voided sale takes its entry back (mirror entry). A void whose sale was never posted is a no-op.</summary>
    public class PosSaleVoidedIntegrationEventHandler : INotificationHandler<PosSaleVoidedIntegrationEvent>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<PosSaleVoidedIntegrationEventHandler> _logger;

        public PosSaleVoidedIntegrationEventHandler(
            IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<PosSaleVoidedIntegrationEventHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task Handle(PosSaleVoidedIntegrationEvent @event, CancellationToken cancellationToken)
        {
            var reason = string.IsNullOrWhiteSpace(@event.Reason) ? "POS sale voided" : $"POS sale voided: {@event.Reason}";
            var reversal = await _posting.TryReverseAsync(_logger, PostingRules.PosSale, @event.TransactionId, reason,
                @event.VoidedAt == default ? DateTime.UtcNow : @event.VoidedAt, cancellationToken);
            if (reversal == null)
                _logger.LogInformation("POS sale {TransactionId} was voided and has no entry to reverse (or it was reversed already)", @event.TransactionId);

            await _uow.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Counted cash that differs from the expected cash is booked to cash over / short.</summary>
    public class PosShiftClosedIntegrationEventHandler : INotificationHandler<PosShiftClosedIntegrationEvent>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<PosShiftClosedIntegrationEventHandler> _logger;

        public PosShiftClosedIntegrationEventHandler(
            IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<PosShiftClosedIntegrationEventHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task Handle(PosShiftClosedIntegrationEvent @event, CancellationToken cancellationToken)
        {
            var request = PostingRules.ForPosShiftVariance(@event.ShiftId, @event.CashVariance ?? 0m,
                @event.ClosedAt == default ? DateTime.UtcNow : @event.ClosedAt);
            if (request == null)
                return;

            await _posting.TryPostAsync(_logger, request, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
        }
    }
}

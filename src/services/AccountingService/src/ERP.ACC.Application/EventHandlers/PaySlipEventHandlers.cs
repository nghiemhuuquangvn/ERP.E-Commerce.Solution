using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.EventHandlers
{
    /// <summary>
    /// HRMService's <c>PaySlipGeneratedEvent</c> (Kafka <c>hrm-topic</c>) reduced to the money (ROADMAP P1-1d). The amounts are 0 on events
    /// published before the ledger posting existed; those cannot be posted.
    /// </summary>
    public class PaySlipGeneratedIntegrationEvent : INotification
    {
        public Guid PaySlipId { get; set; }
        public string PayPeriod { get; set; } = string.Empty;
        public decimal GrossSalary { get; set; }
        public decimal OvertimePay { get; set; }
        public decimal Deductions { get; set; }
        public decimal NetPay { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    public class PaySlipPaidIntegrationEvent : INotification
    {
        public Guid PaySlipId { get; set; }
        public string PayPeriod { get; set; } = string.Empty;
        public decimal NetPay { get; set; }
        public DateTime PaidOn { get; set; }
    }

    public class PaySlipVoidedIntegrationEvent : INotification
    {
        public Guid PaySlipId { get; set; }
        public string? Reason { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    /// <summary>Dr Salary expense (gross + overtime) / Cr Salaries payable (net) / Cr Payroll withholdings payable (deductions).</summary>
    public class PaySlipGeneratedIntegrationEventHandler : INotificationHandler<PaySlipGeneratedIntegrationEvent>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<PaySlipGeneratedIntegrationEventHandler> _logger;

        public PaySlipGeneratedIntegrationEventHandler(
            IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<PaySlipGeneratedIntegrationEventHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task Handle(PaySlipGeneratedIntegrationEvent @event, CancellationToken cancellationToken)
        {
            if (@event.GrossSalary + @event.OvertimePay <= 0m)
            {
                _logger.LogInformation("Pay slip {PaySlipId} predates the amounts on the event (or is empty); nothing posted", @event.PaySlipId);
                return;
            }

            var request = PostingRules.ForPaySlip(@event.PaySlipId, @event.PayPeriod, @event.GrossSalary, @event.OvertimePay,
                @event.Deductions, @event.PeriodEnd, DateTime.UtcNow);
            if (request == null)
            {
                _logger.LogError("Pay slip {PaySlipId} has deductions above its gross pay; no journal entry was posted", @event.PaySlipId);
                return;
            }

            await _posting.TryPostAsync(_logger, request, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Dr Salaries payable / Cr Bank, only for a slip whose accrual is in the books (a slip from before the posting existed has none).</summary>
    public class PaySlipPaidIntegrationEventHandler : INotificationHandler<PaySlipPaidIntegrationEvent>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<PaySlipPaidIntegrationEventHandler> _logger;

        public PaySlipPaidIntegrationEventHandler(
            IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<PaySlipPaidIntegrationEventHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task Handle(PaySlipPaidIntegrationEvent @event, CancellationToken cancellationToken)
        {
            var accrual = await _uow.JournalEntries.GetBySourceKeyAsync(
                JournalEntry.BuildSourceKey(PostingRules.PaySlip, @event.PaySlipId), cancellationToken);
            if (accrual == null)
            {
                _logger.LogWarning("Pay slip {PaySlipId} was paid but its salary entry was never posted; the payment is not booked", @event.PaySlipId);
                return;
            }

            var request = PostingRules.ForPaySlipPayment(@event.PaySlipId, @event.PayPeriod, @event.NetPay,
                @event.PaidOn == default ? DateTime.UtcNow : @event.PaidOn);
            if (request == null)
                return;

            await _posting.TryPostAsync(_logger, request, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>A cancelled (unpaid) pay slip takes its entry back.</summary>
    public class PaySlipVoidedIntegrationEventHandler : INotificationHandler<PaySlipVoidedIntegrationEvent>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<PaySlipVoidedIntegrationEventHandler> _logger;

        public PaySlipVoidedIntegrationEventHandler(
            IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<PaySlipVoidedIntegrationEventHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task Handle(PaySlipVoidedIntegrationEvent @event, CancellationToken cancellationToken)
        {
            var reason = string.IsNullOrWhiteSpace(@event.Reason) ? "Pay slip cancelled" : $"Pay slip cancelled: {@event.Reason}";
            var reversal = await _posting.TryReverseAsync(_logger, PostingRules.PaySlip, @event.PaySlipId, reason,
                @event.OccurredAt == default ? DateTime.UtcNow : @event.OccurredAt, cancellationToken);
            if (reversal == null)
                _logger.LogInformation("Pay slip {PaySlipId} was cancelled and has no entry to reverse (or it was reversed already)", @event.PaySlipId);

            await _uow.SaveChangesAsync(cancellationToken);
        }
    }
}

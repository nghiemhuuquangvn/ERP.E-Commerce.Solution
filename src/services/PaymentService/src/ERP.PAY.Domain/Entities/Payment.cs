using ERP.Shared.Core.Domain;
using ERP.PAY.Domain.Enums;
using ERP.PAY.Domain.Events;

namespace ERP.PAY.Domain.Entities;

public class Payment : AggregateRoot
{
    private readonly List<Refund> _refunds = new();

    public Guid OrderId { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "USD";
    public PaymentMethod Method { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? TransactionRef { get; private set; }
    public string? GatewayTransactionId { get; private set; }
    public string? GatewayProvider { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime? AuthorizedAt { get; private set; }
    public DateTime? CapturedAt { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public bool IsFlaggedForReview { get; private set; }
    public string? FraudReviewReason { get; private set; }
    public Guid? SettlementId { get; private set; }
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    private Payment() { }

    public static Payment Initiate(Guid orderId, decimal amount, PaymentMethod method, string currency = "USD", Guid? invoiceId = null)
    {
        if (amount <= 0)
            throw new InvalidOperationException("Payment amount must be positive.");

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            InvoiceId = invoiceId,
            Amount = amount,
            Currency = currency,
            Method = method,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        payment.AddDomainEvent(new PaymentInitiatedEvent(payment.Id, orderId, amount, currency, method));
        return payment;
    }

    public void AssignSettlement(Guid settlementId)
    {
        if (Status != PaymentStatus.Captured)
            throw new InvalidOperationException($"Cannot assign a settlement to a payment in {Status} state.");

        SettlementId = settlementId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void LinkInvoice(Guid invoiceId)
    {
        InvoiceId = invoiceId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignTransactionRef(string transactionRef)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Cannot assign a transaction reference to a payment in {Status} state.");

        TransactionRef = transactionRef;
        UpdatedAt = DateTime.UtcNow;
    }

    public void FlagForReview(string reason)
    {
        IsFlaggedForReview = true;
        FraudReviewReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Authorize(string gatewayTransactionId, string gatewayProvider)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Cannot authorize payment in {Status} state.");

        GatewayTransactionId = gatewayTransactionId;
        GatewayProvider = gatewayProvider;
        Status = PaymentStatus.Authorized;
        AuthorizedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentAuthorizedEvent(Id, OrderId, Amount, Currency));
    }

    public void Capture()
    {
        if (Status != PaymentStatus.Authorized)
            throw new InvalidOperationException($"Cannot capture payment in {Status} state.");

        Status = PaymentStatus.Captured;
        CapturedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentProcessedEvent(Id, OrderId, Amount, Currency, Method, CapturedAt.Value));
    }

    public void Fail(string reason)
    {
        if (Status is PaymentStatus.Captured or PaymentStatus.Refunded)
            throw new InvalidOperationException($"Cannot fail payment in {Status} state.");

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        FailedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentFailedEvent(Id, OrderId, Amount, reason));
    }

    public Refund AddRefund(decimal refundAmount, string reason)
    {
        if (Status != PaymentStatus.Captured && Status != PaymentStatus.PartiallyRefunded)
            throw new InvalidOperationException($"Cannot refund payment in {Status} state.");

        var totalRefunded = _refunds.Where(r => r.Status == RefundStatus.Completed).Sum(r => r.Amount);
        if (totalRefunded + refundAmount > Amount)
            throw new InvalidOperationException("Refund amount exceeds original payment amount.");

        var refund = Refund.Create(Id, refundAmount, reason);
        _refunds.Add(refund);

        Status = (totalRefunded + refundAmount >= Amount)
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded;

        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new PaymentRefundedEvent(Id, OrderId, refundAmount, reason));
        return refund;
    }

    /// <summary>
    /// AddRefund optimistically marks the payment Refunded/PartiallyRefunded before
    /// the gateway has confirmed the money actually moved. Call this after the
    /// gateway call resolves (Refund.MarkAsCompleted/MarkAsFailed) so Status reflects
    /// only refunds the gateway actually confirmed — a refund the gateway declines
    /// must not leave the payment looking refunded.
    /// </summary>
    public void ReconcileStatusWithConfirmedRefunds()
    {
        var totalCompleted = _refunds.Where(r => r.Status == RefundStatus.Completed).Sum(r => r.Amount);

        Status = totalCompleted <= 0
            ? PaymentStatus.Captured
            : totalCompleted >= Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;

        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status is PaymentStatus.Captured or PaymentStatus.Refunded)
            throw new InvalidOperationException($"Cannot cancel payment in {Status} state.");

        Status = PaymentStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}

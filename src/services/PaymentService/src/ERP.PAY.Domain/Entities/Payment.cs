using ERP.Shared.Core.Domain;
using ERP.PAY.Domain.Enums;
using ERP.PAY.Domain.Events;

namespace ERP.PAY.Domain.Entities;

public class Payment : AggregateRoot
{
    private readonly List<Refund> _refunds = new();

    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "USD";
    public PaymentMethod Method { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? GatewayTransactionId { get; private set; }
    public string? GatewayProvider { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime? AuthorizedAt { get; private set; }
    public DateTime? CapturedAt { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    private Payment() { }

    public static Payment Initiate(Guid orderId, decimal amount, PaymentMethod method, string currency = "USD")
    {
        if (amount <= 0)
            throw new InvalidOperationException("Payment amount must be positive.");

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = amount,
            Currency = currency,
            Method = method,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        payment.AddDomainEvent(new PaymentInitiatedEvent(payment.Id, orderId, amount, currency, method));
        return payment;
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

    public void Cancel()
    {
        if (Status is PaymentStatus.Captured or PaymentStatus.Refunded)
            throw new InvalidOperationException($"Cannot cancel payment in {Status} state.");

        Status = PaymentStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }
}

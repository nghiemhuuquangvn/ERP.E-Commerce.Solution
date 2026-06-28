using ERP.Shared.Core.Domain;
using ERP.PAY.Domain.Enums;

namespace ERP.PAY.Domain.Events;

public record PaymentInitiatedEvent(
    Guid PaymentId, Guid OrderId, decimal Amount, string Currency, PaymentMethod Method) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record PaymentAuthorizedEvent(
    Guid PaymentId, Guid OrderId, decimal Amount, string Currency) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record PaymentProcessedEvent(
    Guid PaymentId, Guid OrderId, decimal Amount, string Currency,
    PaymentMethod Method, DateTime ProcessedAt) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record PaymentFailedEvent(
    Guid PaymentId, Guid OrderId, decimal Amount, string FailureReason) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record PaymentRefundedEvent(
    Guid PaymentId, Guid OrderId, decimal RefundAmount, string Reason) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

using ERP.Shared.Core.Domain;
using ERP.PAY.Domain.Enums;

namespace ERP.PAY.Domain.Events;

public record PaymentInitiatedEvent(
    Guid PaymentId, Guid OrderId, decimal Amount, string Currency, PaymentMethod Method) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record PaymentAuthorizedEvent(
    Guid PaymentId, Guid OrderId, decimal Amount, string Currency) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record PaymentProcessedEvent(
    Guid PaymentId, Guid OrderId, decimal Amount, string Currency,
    PaymentMethod Method, DateTime ProcessedAt) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record PaymentFailedEvent(
    Guid PaymentId, Guid OrderId, decimal Amount, string FailureReason) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>
/// A refund the payment gateway CONFIRMED (ROADMAP P1-1e): raised by <c>Payment.CompleteRefund</c> only, never for a pending or failed refund.
/// <c>RefundId</c> is the idempotency key of the ledger entry; it is null on events published before this change, which were also raised
/// before the gateway answered and must not be booked. Reporting reads PaymentId / OrderId / RefundAmount / Reason: keep those names.
/// </summary>
public record PaymentRefundedEvent(
    Guid PaymentId, Guid OrderId, decimal RefundAmount, string Reason,
    Guid? RefundId = null, string? Currency = null, PaymentMethod? Method = null,
    DateTime? ProcessedAt = null, string? GatewayRefundId = null) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

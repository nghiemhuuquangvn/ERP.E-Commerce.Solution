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

public record PaymentRefundedEvent(
    Guid PaymentId, Guid OrderId, decimal RefundAmount, string Reason) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

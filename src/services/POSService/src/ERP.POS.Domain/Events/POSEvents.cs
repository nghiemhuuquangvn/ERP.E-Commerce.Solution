using ERP.Shared.Core.Domain;

namespace ERP.POS.Domain.Events;

/// <summary>A sold (or returned) quantity — WarehouseService takes stock out (or back in) per line.</summary>
public record PosSaleLine(Guid ProductId, string ProductName, int Quantity);

public record TransactionCreatedEvent(
    Guid TransactionId, Guid TerminalId, Guid CashierId,
    decimal TotalAmount, string PaymentMethod, DateTime CompletedAt,
    IReadOnlyList<PosSaleLine> Lines) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>
/// A completed sale was voided: the goods came back, so WarehouseService puts the lines
/// back into stock. Not raised when an open (never completed) transaction is voided.
/// </summary>
public record TransactionVoidedEvent(
    Guid TransactionId, Guid TerminalId, string Reason, DateTime VoidedAt,
    IReadOnlyList<PosSaleLine> Lines) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record CashShiftClosedEvent(
    Guid ShiftId, Guid TerminalId, Guid CashierId,
    decimal TotalSales, int TransactionCount, DateTime ClosedAt) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

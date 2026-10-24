using ERP.Shared.Core.Domain;

namespace ERP.POS.Domain.Events;

/// <summary>A sold (or returned) quantity — WarehouseService takes stock out (or back in) per line.</summary>
public record PosSaleLine(Guid ProductId, string ProductName, int Quantity);

/// <summary>How much of a sale was paid with one tender (method names are free text; "Cash" is the cash drawer).</summary>
public record PosTender(string Method, decimal Amount);

/// <summary>
/// The money of a sale, as POS charged it (ROADMAP P1-1c). Tax is exclusive and is NOT reduced by the discount, so the ledger
/// splits the sale as: tenders = <c>SubTotal - DiscountAmount + TaxAmount</c> = revenue (<c>SubTotal - DiscountAmount</c>) + VAT.
/// </summary>
public record PosSaleAmounts(decimal SubTotal, decimal DiscountAmount, decimal TaxAmount, IReadOnlyList<PosTender> Tenders);

public record TransactionCreatedEvent(
    Guid TransactionId, Guid TerminalId, Guid CashierId,
    decimal TotalAmount, string PaymentMethod, DateTime CompletedAt,
    IReadOnlyList<PosSaleLine> Lines,
    PosSaleAmounts? Amounts = null, Guid? CashShiftId = null, Guid? ExternalPaymentId = null) : IDomainEvent, ITenantEvent
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
    IReadOnlyList<PosSaleLine> Lines,
    PosSaleAmounts? Amounts = null, Guid? CashShiftId = null) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>
/// <c>CountedCash</c> is what the cashier counted; <c>ExpectedCash</c> = opening float + cash tenders of the shift's completed
/// (not voided) sales; <c>CashVariance</c> = counted - expected (negative = shortage). The three are null on events published
/// before ROADMAP P1-1c.
/// </summary>
public record CashShiftClosedEvent(
    Guid ShiftId, Guid TerminalId, Guid CashierId,
    decimal TotalSales, int TransactionCount, DateTime ClosedAt,
    decimal? OpeningBalance = null, decimal? CountedCash = null, decimal? ExpectedCash = null, decimal? CashVariance = null) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

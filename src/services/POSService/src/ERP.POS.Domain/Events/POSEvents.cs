using ERP.Shared.Core.Domain;

namespace ERP.POS.Domain.Events;

public record TransactionCreatedEvent(
    Guid TransactionId, Guid TerminalId, Guid CashierId,
    decimal TotalAmount, string PaymentMethod, DateTime CompletedAt) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record CashShiftClosedEvent(
    Guid ShiftId, Guid TerminalId, Guid CashierId,
    decimal TotalSales, int TransactionCount, DateTime ClosedAt) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

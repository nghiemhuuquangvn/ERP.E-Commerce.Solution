using ERP.Shared.Core.Domain;
using ERP.POS.Domain.Enums;
using ERP.POS.Domain.Events;

namespace ERP.POS.Domain.Entities;

public class CashShift : AggregateRoot
{
    public Guid TerminalId { get; private set; }
    public Guid CashierId { get; private set; }
    public decimal OpeningBalance { get; private set; }
    public decimal? ClosingBalance { get; private set; }
    public decimal TotalSales { get; private set; }
    public int TransactionCount { get; private set; }
    public CashShiftStatus Status { get; private set; }
    public DateTime OpenedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public string? Notes { get; private set; }

    private CashShift() { }

    public static CashShift Open(Guid terminalId, Guid cashierId, decimal openingBalance)
    {
        return new CashShift
        {
            Id = Guid.NewGuid(),
            TerminalId = terminalId,
            CashierId = cashierId,
            OpeningBalance = openingBalance,
            TotalSales = 0,
            TransactionCount = 0,
            Status = CashShiftStatus.Open,
            OpenedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void RecordTransaction(decimal amount)
    {
        TotalSales += amount;
        TransactionCount++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Close(decimal closingBalance, string? notes = null)
    {
        if (Status != CashShiftStatus.Open)
            throw new InvalidOperationException("Shift is not open.");

        ClosingBalance = closingBalance;
        Notes = notes;
        Status = CashShiftStatus.Closed;
        ClosedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new CashShiftClosedEvent(Id, TerminalId, CashierId, TotalSales, TransactionCount, ClosedAt.Value));
    }
}

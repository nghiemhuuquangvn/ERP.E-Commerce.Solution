using ERP.Shared.Core.Domain;
using ERP.POS.Domain.Enums;
using ERP.POS.Domain.Events;

namespace ERP.POS.Domain.Entities;

public class CashShift : AggregateRoot
{
    private readonly List<CashDenominationLine> _denominations = new();

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
    public IReadOnlyCollection<CashDenominationLine> Denominations => _denominations.AsReadOnly();

    private CashShift() { }

    public static CashShift Open(Guid terminalId, Guid cashierId, decimal openingBalance,
        List<(decimal denomination, int count)>? openingCounts = null)
    {
        var shift = new CashShift
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

        if (openingCounts != null)
        {
            var countedTotal = openingCounts.Sum(c => c.denomination * c.count);
            if (countedTotal != openingBalance)
                throw new InvalidOperationException($"Counted denomination total {countedTotal} does not match opening balance {openingBalance}.");

            foreach (var (denomination, count) in openingCounts)
                shift._denominations.Add(new CashDenominationLine(shift.Id, DenominationStage.Opening, denomination, count));
        }

        return shift;
    }

    public void RecordTransaction(decimal amount)
    {
        TotalSales += amount;
        TransactionCount++;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>A completed sale of this shift was voided while the shift is still open: it no longer counts as a sale.</summary>
    public void UndoTransaction(decimal amount)
    {
        if (Status != CashShiftStatus.Open)
            return; // the closed event already went out; the void itself reverses the sale in the ledger
        TotalSales -= amount;
        TransactionCount = Math.Max(0, TransactionCount - 1);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <param name="cashSalesTotal">Cash tenders of the shift's completed (not voided) sales, for the expected-cash figure on the event.</param>
    public void Close(decimal closingBalance, string? notes = null, List<(decimal denomination, int count)>? closingCounts = null, decimal cashSalesTotal = 0m)
    {
        if (Status != CashShiftStatus.Open)
            throw new InvalidOperationException("Shift is not open.");

        if (closingCounts != null)
        {
            var countedTotal = closingCounts.Sum(c => c.denomination * c.count);
            if (countedTotal != closingBalance)
                throw new InvalidOperationException($"Counted denomination total {countedTotal} does not match closing balance {closingBalance}.");

            foreach (var (denomination, count) in closingCounts)
                _denominations.Add(new CashDenominationLine(Id, DenominationStage.Closing, denomination, count));
        }

        ClosingBalance = closingBalance;
        Notes = notes;
        Status = CashShiftStatus.Closed;
        ClosedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new CashShiftClosedEvent(Id, TerminalId, CashierId, TotalSales, TransactionCount, ClosedAt.Value,
            OpeningBalance, closingBalance, ExpectedCashBalance(cashSalesTotal), closingBalance - ExpectedCashBalance(cashSalesTotal)));
    }

    /// <summary>Expected cash on hand: opening float + cash-tendered sales this shift.</summary>
    public decimal ExpectedCashBalance(decimal cashSalesTotal) => OpeningBalance + cashSalesTotal;

    public decimal? CashVariance(decimal cashSalesTotal) =>
        ClosingBalance.HasValue ? ClosingBalance.Value - ExpectedCashBalance(cashSalesTotal) : null;
}

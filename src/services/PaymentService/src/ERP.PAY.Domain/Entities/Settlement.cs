using ERP.Shared.Core.Domain;
using ERP.PAY.Domain.Enums;

namespace ERP.PAY.Domain.Entities;

public class Settlement : AggregateRoot
{
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }
    public decimal TotalAmount { get; private set; }
    public int TransactionCount { get; private set; }
    public SettlementStatus Status { get; private set; }
    public DateTime? SettledAt { get; private set; }
    public string? Notes { get; private set; }

    private Settlement() { }

    public static Settlement Create(DateTime periodStart, DateTime periodEnd, decimal totalAmount, int transactionCount)
    {
        return new Settlement
        {
            Id = Guid.NewGuid(),
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            TotalAmount = totalAmount,
            TransactionCount = transactionCount,
            Status = SettlementStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Process()
    {
        if (Status != SettlementStatus.Pending)
            throw new InvalidOperationException("Settlement already processed.");

        Status = SettlementStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete(string? notes = null)
    {
        Status = SettlementStatus.Settled;
        SettledAt = DateTime.UtcNow;
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }
}

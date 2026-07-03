using ERP.Shared.Core.Domain;

namespace ERP.PAY.Domain.Entities;

public enum InstallmentStatus
{
    Pending = 0,
    Paid = 1,
    Overdue = 2
}

public class InstallmentLine
{
    public Guid Id { get; private set; }
    public int SequenceNo { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime DueDate { get; private set; }
    public InstallmentStatus Status { get; private set; }
    public DateTime? PaidAt { get; private set; }

    private InstallmentLine() { }

    public InstallmentLine(int sequenceNo, decimal amount, DateTime dueDate)
    {
        Id = Guid.NewGuid();
        SequenceNo = sequenceNo;
        Amount = amount;
        DueDate = dueDate;
        Status = InstallmentStatus.Pending;
    }

    public void MarkPaid()
    {
        if (Status == InstallmentStatus.Paid)
            throw new InvalidOperationException($"Installment #{SequenceNo} is already paid.");

        Status = InstallmentStatus.Paid;
        PaidAt = DateTime.UtcNow;
    }

    public void MarkOverdue()
    {
        if (Status == InstallmentStatus.Pending && DueDate < DateTime.UtcNow)
            Status = InstallmentStatus.Overdue;
    }
}

public class InstallmentPlan : AggregateRoot
{
    private readonly List<InstallmentLine> _installments = new();

    public Guid PaymentId { get; private set; }
    public int TotalInstallments { get; private set; }
    public decimal TotalAmount { get; private set; }
    public IReadOnlyCollection<InstallmentLine> Installments => _installments.AsReadOnly();

    private InstallmentPlan() { }

    public static InstallmentPlan Create(Guid paymentId, decimal totalAmount, int totalInstallments, DateTime firstDueDate, int intervalDays)
    {
        if (totalInstallments < 2)
            throw new InvalidOperationException("An installment plan requires at least 2 installments.");
        if (totalAmount <= 0)
            throw new InvalidOperationException("Installment plan amount must be positive.");

        var plan = new InstallmentPlan
        {
            Id = Guid.NewGuid(),
            PaymentId = paymentId,
            TotalInstallments = totalInstallments,
            TotalAmount = totalAmount,
            CreatedAt = DateTime.UtcNow
        };

        var baseAmount = Math.Round(totalAmount / totalInstallments, 2, MidpointRounding.ToEven);
        var allocated = 0m;

        for (var i = 1; i <= totalInstallments; i++)
        {
            var isLast = i == totalInstallments;
            var amount = isLast ? totalAmount - allocated : baseAmount;
            allocated += amount;

            plan._installments.Add(new InstallmentLine(i, amount, firstDueDate.AddDays(intervalDays * (i - 1))));
        }

        return plan;
    }

    public void RecordInstallmentPayment(int sequenceNo)
    {
        var line = _installments.FirstOrDefault(i => i.SequenceNo == sequenceNo)
            ?? throw new InvalidOperationException($"Installment #{sequenceNo} not found in this plan.");

        line.MarkPaid();
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsFullyPaid() => _installments.All(i => i.Status == InstallmentStatus.Paid);
}

namespace ERP.POS.Domain.Entities;

/// <summary>
/// One payment line within a transaction — split payment (e.g. part cash, part card)
/// is modeled as multiple tenders whose Amounts must sum to Transaction.TotalAmount,
/// rather than the single free-text PaymentMethod field only supporting one method.
/// </summary>
public class TransactionTender
{
    public Guid Id { get; private set; }
    public Guid TransactionId { get; private set; }
    public string PaymentMethod { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }

    private TransactionTender() { }

    public TransactionTender(Guid transactionId, string paymentMethod, decimal amount)
    {
        if (amount <= 0)
            throw new InvalidOperationException("Tender amount must be positive.");

        Id = Guid.NewGuid();
        TransactionId = transactionId;
        PaymentMethod = paymentMethod;
        Amount = amount;
    }
}

using ERP.Shared.Core.Domain;

namespace ERP.POS.Domain.Entities;

public class TransactionItem : BaseEntity
{
    public Guid TransactionId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TaxRate { get; private set; }
    public decimal LineTotal => UnitPrice * Quantity;
    public decimal TaxAmount => LineTotal * TaxRate / 100;

    private TransactionItem() { }

    public static TransactionItem Create(Guid transactionId, Guid productId, string productName,
        int quantity, decimal unitPrice, decimal taxRate = 0)
    {
        return new TransactionItem
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            ProductId = productId,
            ProductName = productName,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TaxRate = taxRate,
            CreatedAt = DateTime.UtcNow
        };
    }
}

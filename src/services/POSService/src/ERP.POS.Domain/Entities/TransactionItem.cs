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
    public decimal DiscountAmount { get; private set; }

    // Stored (not computed-only) — the SQL schema models LineTotal/TaxAmount as real
    // NOT NULL columns; a pure expression-bodied property is invisible to EF (no
    // setter), which would omit them from INSERT/UPDATE entirely and violate those
    // NOT NULL constraints against a DB actually provisioned from that script.
    public decimal LineTotal { get; private set; }
    public decimal TaxAmount { get; private set; }

    private TransactionItem() { }

    public static TransactionItem Create(Guid transactionId, Guid productId, string productName,
        int quantity, decimal unitPrice, decimal taxRate = 0)
    {
        var item = new TransactionItem
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

        item.Recalculate();
        return item;
    }

    private void Recalculate()
    {
        LineTotal = UnitPrice * Quantity;
        TaxAmount = (LineTotal - DiscountAmount) * TaxRate / 100;
    }
}

using ERP.Shared.Core.Domain;
using ERP.POS.Domain.Enums;
using ERP.POS.Domain.Events;

namespace ERP.POS.Domain.Entities;

public class Transaction : AggregateRoot
{
    private readonly List<TransactionItem> _items = new();

    public Guid TerminalId { get; private set; }
    public Guid CashierId { get; private set; }
    public Guid? CashShiftId { get; private set; }
    public decimal SubTotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string? PaymentMethod { get; private set; }
    public TransactionStatus Status { get; private set; }
    public string? VoidReason { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public IReadOnlyCollection<TransactionItem> Items => _items.AsReadOnly();

    private Transaction() { }

    public static Transaction Open(Guid terminalId, Guid cashierId, Guid? cashShiftId = null)
    {
        return new Transaction
        {
            Id = Guid.NewGuid(),
            TerminalId = terminalId,
            CashierId = cashierId,
            CashShiftId = cashShiftId,
            Status = TransactionStatus.Open,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AddItem(Guid productId, string productName, int quantity, decimal unitPrice, decimal taxRate = 0)
    {
        if (Status != TransactionStatus.Open)
            throw new InvalidOperationException("Cannot add items to a non-open transaction.");

        var existing = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existing != null)
        {
            _items.Remove(existing);
            _items.Add(TransactionItem.Create(Id, productId, productName, existing.Quantity + quantity, unitPrice, taxRate));
        }
        else
        {
            _items.Add(TransactionItem.Create(Id, productId, productName, quantity, unitPrice, taxRate));
        }

        Recalculate();
    }

    public void RemoveItem(Guid productId)
    {
        var item = _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new InvalidOperationException($"Product {productId} not in transaction.");
        _items.Remove(item);
        Recalculate();
    }

    public void ApplyDiscount(decimal discountAmount)
    {
        if (discountAmount > SubTotal)
            throw new InvalidOperationException("Discount cannot exceed subtotal.");
        DiscountAmount = discountAmount;
        Recalculate();
    }

    public void Complete(string paymentMethod)
    {
        if (Status != TransactionStatus.Open)
            throw new InvalidOperationException($"Cannot complete transaction in {Status} state.");
        if (!_items.Any())
            throw new InvalidOperationException("Cannot complete empty transaction.");

        PaymentMethod = paymentMethod;
        Status = TransactionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new TransactionCreatedEvent(Id, TerminalId, CashierId, TotalAmount, paymentMethod, CompletedAt.Value));
    }

    public void Void(string reason)
    {
        if (Status == TransactionStatus.Voided)
            throw new InvalidOperationException("Transaction already voided.");

        Status = TransactionStatus.Voided;
        VoidReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    private void Recalculate()
    {
        SubTotal = _items.Sum(i => i.LineTotal);
        TaxAmount = _items.Sum(i => i.TaxAmount);
        TotalAmount = SubTotal - DiscountAmount + TaxAmount;
        UpdatedAt = DateTime.UtcNow;
    }
}

using ERP.Shared.Core.Domain;
using ERP.POS.Domain.Enums;
using ERP.POS.Domain.Events;

namespace ERP.POS.Domain.Entities;

public class Transaction : AggregateRoot
{
    private readonly List<TransactionItem> _items = new();
    private readonly List<TransactionTender> _tenders = new();

    public Guid TerminalId { get; private set; }
    public Guid CashierId { get; private set; }
    public Guid? CashShiftId { get; private set; }
    public Guid? PromotionId { get; private set; }
    public decimal SubTotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string? PaymentMethod { get; private set; }
    public TransactionStatus Status { get; private set; }
    public string? VoidReason { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? VoidedAt { get; private set; }
    public Guid? ExternalPaymentId { get; private set; }
    public IReadOnlyCollection<TransactionItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<TransactionTender> Tenders => _tenders.AsReadOnly();

    private Transaction() { }

    public static Transaction Open(Guid terminalId, Guid cashierId, Guid? cashShiftId = null, Guid? id = null)
    {
        return new Transaction
        {
            Id = id ?? Guid.NewGuid(),
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

    public void ApplyDiscount(decimal discountAmount, Guid? promotionId = null)
    {
        if (discountAmount > SubTotal)
            throw new InvalidOperationException("Discount cannot exceed subtotal.");
        DiscountAmount = discountAmount;
        PromotionId = promotionId;
        Recalculate();
    }

    /// <summary>
    /// Single-method completion — kept for backward compatibility with existing
    /// callers/tests. Delegates to CompleteWithTenders with one tender for the full
    /// amount so both paths share the same validation and event-raising logic.
    /// </summary>
    public void Complete(string paymentMethod) =>
        CompleteWithTenders(new List<(string method, decimal amount)> { (paymentMethod, TotalAmount) });

    /// <summary>
    /// Split payment — e.g. part cash, part card. Tenders must sum to exactly
    /// TotalAmount; PaymentMethod is recorded as "Mixed" when more than one tender is
    /// used (a single tender keeps its own method name for backward-compatible display).
    /// </summary>
    public void CompleteWithTenders(List<(string method, decimal amount)> tenders)
    {
        if (Status != TransactionStatus.Open)
            throw new InvalidOperationException($"Cannot complete transaction in {Status} state.");
        if (!_items.Any())
            throw new InvalidOperationException("Cannot complete empty transaction.");
        if (tenders == null || tenders.Count == 0)
            throw new InvalidOperationException("At least one payment tender is required.");

        var tenderTotal = tenders.Sum(t => t.amount);
        if (tenderTotal != TotalAmount)
            throw new InvalidOperationException($"Tender total {tenderTotal} does not match transaction total {TotalAmount}.");

        foreach (var (method, amount) in tenders)
            _tenders.Add(new TransactionTender(Id, method, amount));

        PaymentMethod = tenders.Count > 1 ? "Mixed" : tenders[0].method;
        Status = TransactionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new TransactionCreatedEvent(Id, TerminalId, CashierId, TotalAmount, PaymentMethod, CompletedAt.Value, SaleLines(),
            SaleAmounts(), CashShiftId, ExternalPaymentId));
    }

    public void ConfirmExternalPayment(Guid externalPaymentId)
    {
        ExternalPaymentId = externalPaymentId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Void(string reason)
    {
        if (Status == TransactionStatus.Voided)
            throw new InvalidOperationException("Transaction already voided.");

        var wasCompleted = Status == TransactionStatus.Completed;

        Status = TransactionStatus.Voided;
        VoidReason = reason;
        VoidedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        // Only a completed sale took stock out; voiding an open cart has nothing to put back.
        if (wasCompleted)
            AddDomainEvent(new TransactionVoidedEvent(Id, TerminalId, reason, VoidedAt.Value, SaleLines(), SaleAmounts(), CashShiftId));
    }

    /// <summary>What the drawer received for this sale: its cash tenders (a sale without tender rows is paid by <see cref="PaymentMethod"/>).</summary>
    public decimal CashTendered => _tenders.Count > 0
        ? _tenders.Where(x => IsCash(x.PaymentMethod)).Sum(x => x.Amount)
        : IsCash(PaymentMethod) ? TotalAmount : 0m;

    public static bool IsCash(string? method) => string.Equals(method?.Trim(), "Cash", StringComparison.OrdinalIgnoreCase);

    private PosSaleAmounts SaleAmounts() =>
        new(SubTotal, DiscountAmount, TaxAmount, _tenders.Select(x => new PosTender(x.PaymentMethod, x.Amount)).ToList());

    private List<PosSaleLine> SaleLines() =>
        _items.Select(i => new PosSaleLine(i.ProductId, i.ProductName, i.Quantity)).ToList();

    private void Recalculate()
    {
        SubTotal = _items.Sum(i => i.LineTotal);
        TaxAmount = _items.Sum(i => i.TaxAmount);
        TotalAmount = SubTotal - DiscountAmount + TaxAmount;
        UpdatedAt = DateTime.UtcNow;
    }
}

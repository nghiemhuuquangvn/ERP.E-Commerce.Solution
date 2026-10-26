using ERP.PO.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Entities;

public enum VendorReturnStatus
{
    Draft = 0,
    Shipped = 1,
    Credited = 2,
    Cancelled = 3
}

/// <summary>
/// Goods sent back to the supplier (ROADMAP P1-8). Shipping publishes an event: Warehouse issues the stock and Accounting reverses the
/// receipt accrual. The supplier's credit note closes it and publishes a second event: Accounting books the debit note.
/// </summary>
public class VendorReturn : BaseEntity
{
    public string ReturnNumber { get; private set; } = string.Empty;
    public Guid PurchaseOrderId { get; private set; }
    public string PONumber { get; private set; } = string.Empty;
    public Guid SupplierId { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public VendorReturnStatus Status { get; private set; } = VendorReturnStatus.Draft;
    public DateTime? ShippedAt { get; private set; }
    public string? CreditNoteNumber { get; private set; }
    public decimal CreditAmount { get; private set; }
    public DateTime? CreditedAt { get; private set; }

    private readonly List<VendorReturnLine> _lines = new();
    public IReadOnlyCollection<VendorReturnLine> Lines => _lines.AsReadOnly();

    public decimal Value => _lines.Sum(l => l.Quantity * l.UnitPrice);

    private VendorReturn() { }

    /// <summary>Lines come with the quantity still returnable per product (received − already returned on this order); more is refused.</summary>
    public static VendorReturn Create(
        string returnNumber, Guid purchaseOrderId, string poNumber, Guid supplierId, Guid tenantId, string currencyCode, string reason,
        IEnumerable<(string ProductCode, string ProductName, decimal Quantity, decimal UnitPrice)> lines,
        IReadOnlyDictionary<string, decimal> returnable)
    {
        if (string.IsNullOrWhiteSpace(returnNumber)) throw new ArgumentException("A return number is required.", nameof(returnNumber));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));

        var ret = new VendorReturn
        {
            ReturnNumber = returnNumber.Trim(),
            PurchaseOrderId = purchaseOrderId,
            PONumber = poNumber,
            SupplierId = supplierId,
            TenantId = tenantId,
            CurrencyCode = currencyCode,
            Reason = reason.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        foreach (var (code, name, quantity, price) in lines)
        {
            if (quantity <= 0) throw new ArgumentException($"Quantity of {code} must be greater than zero.");
            if (!returnable.TryGetValue(code, out var max) || quantity > max)
                throw new InvalidOperationException($"Cannot return {quantity:0.##} of {code}: only {(returnable.TryGetValue(code, out var m) ? m : 0):0.##} can still be returned.");
            if (ret._lines.Any(l => l.ProductCode.Equals(code, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"{code} appears twice on the return.");
            ret._lines.Add(VendorReturnLine.Create(ret.Id, tenantId, code, name, quantity, price));
        }
        if (ret._lines.Count == 0) throw new ArgumentException("A return needs at least one line.");
        return ret;
    }

    public void Ship()
    {
        if (Status != VendorReturnStatus.Draft) throw new InvalidOperationException($"Only a draft return can be shipped (current status: {Status}).");
        Status = VendorReturnStatus.Shipped;
        ShippedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new VendorReturnShippedDomainEvent(
            Id, ReturnNumber, PurchaseOrderId, PONumber, SupplierId, CurrencyCode,
            _lines.Select(l => new VendorReturnShippedDomainEvent.ReturnedLine { ProductCode = l.ProductCode, Quantity = l.Quantity, UnitPrice = l.UnitPrice }).ToList()));
    }

    public void RecordCredit(string creditNoteNumber, decimal creditAmount)
    {
        if (Status != VendorReturnStatus.Shipped) throw new InvalidOperationException($"A credit note can only be recorded for a shipped return (current status: {Status}).");
        if (string.IsNullOrWhiteSpace(creditNoteNumber)) throw new ArgumentException("The supplier's credit note number is required.", nameof(creditNoteNumber));
        if (creditAmount <= 0) throw new ArgumentException("The credit amount must be greater than zero.", nameof(creditAmount));

        Status = VendorReturnStatus.Credited;
        CreditNoteNumber = creditNoteNumber.Trim();
        CreditAmount = creditAmount;
        CreditedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new VendorReturnCreditedDomainEvent(Id, ReturnNumber, PurchaseOrderId, PONumber, SupplierId, CurrencyCode, CreditNoteNumber, creditAmount));
    }

    public void Cancel()
    {
        if (Status != VendorReturnStatus.Draft) throw new InvalidOperationException($"Only a draft return can be cancelled (current status: {Status}).");
        Status = VendorReturnStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }
}

public class VendorReturnLine : BaseEntity
{
    public Guid ReturnId { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    private VendorReturnLine() { }

    internal static VendorReturnLine Create(Guid returnId, Guid tenantId, string productCode, string productName, decimal quantity, decimal unitPrice) =>
        new() { ReturnId = returnId, TenantId = tenantId, ProductCode = productCode, ProductName = productName, Quantity = quantity, UnitPrice = unitPrice, CreatedAt = DateTime.UtcNow };
}

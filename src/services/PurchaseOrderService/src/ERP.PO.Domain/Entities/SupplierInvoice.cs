using ERP.PO.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Entities;

public enum SupplierInvoiceStatus
{
    Recorded = 0,
    Matched = 1,
    OnHold = 2,
    Approved = 3,
    Rejected = 4
}

/// <summary>Per-tenant tolerances of the three-way match (ROADMAP P1-8). Defaults: 0 % quantity, 2 % price.</summary>
public class PurchasingSettings : BaseEntity
{
    public decimal QuantityTolerancePercent { get; private set; }
    public decimal PriceTolerancePercent { get; private set; } = 2m;
    /// <summary>A matched invoice is approved without a person pressing the button (never an invoice on hold).</summary>
    public bool AutoApproveMatched { get; private set; }

    private PurchasingSettings() { }

    public static PurchasingSettings CreateDefault(Guid tenantId) => new() { TenantId = tenantId, CreatedAt = DateTime.UtcNow };

    public void Update(decimal quantityTolerancePercent, decimal priceTolerancePercent, bool autoApproveMatched)
    {
        if (quantityTolerancePercent is < 0 or > 100) throw new ArgumentException("The quantity tolerance is a percentage between 0 and 100.");
        if (priceTolerancePercent is < 0 or > 100) throw new ArgumentException("The price tolerance is a percentage between 0 and 100.");
        QuantityTolerancePercent = quantityTolerancePercent;
        PriceTolerancePercent = priceTolerancePercent;
        AutoApproveMatched = autoApproveMatched;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// An invoice from a supplier for goods on a purchase order, kept so it can be matched against the order and the receipts and then
/// become the accounts-payable bill (ROADMAP P1-8). Before this the bill was created for the whole order when it was approved.
/// </summary>
public class SupplierInvoice : BaseEntity
{
    public string InvoiceNumber { get; private set; } = string.Empty;
    public Guid SupplierId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public string PONumber { get; private set; } = string.Empty;
    public DateTime InvoiceDate { get; private set; }
    public DateTime DueDate { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public decimal NetAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount => NetAmount + TaxAmount;
    public SupplierInvoiceStatus Status { get; private set; } = SupplierInvoiceStatus.Recorded;
    public string? HoldReasons { get; private set; }
    public string? ApprovalReason { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public decimal ReceivedValue { get; private set; }

    private readonly List<SupplierInvoiceLine> _lines = new();
    public IReadOnlyCollection<SupplierInvoiceLine> Lines => _lines.AsReadOnly();

    private readonly List<SupplierInvoiceMatchLine> _matchLines = new();
    public IReadOnlyCollection<SupplierInvoiceMatchLine> MatchLines => _matchLines.AsReadOnly();

    private SupplierInvoice() { }

    public static SupplierInvoice Record(
        string invoiceNumber, Guid supplierId, Guid purchaseOrderId, string poNumber, Guid tenantId, DateTime invoiceDate, DateTime dueDate,
        string currencyCode, decimal taxAmount, IEnumerable<(string ProductCode, decimal Quantity, decimal UnitPrice)> lines)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber)) throw new ArgumentException("The supplier's invoice number is required.", nameof(invoiceNumber));
        if (string.IsNullOrWhiteSpace(currencyCode)) throw new ArgumentException("A currency is required.", nameof(currencyCode));
        if (dueDate.Date < invoiceDate.Date) throw new ArgumentException("The due date cannot be before the invoice date.", nameof(dueDate));
        if (taxAmount < 0) throw new ArgumentException("The tax cannot be negative.", nameof(taxAmount));

        var invoice = new SupplierInvoice
        {
            InvoiceNumber = invoiceNumber.Trim(),
            SupplierId = supplierId,
            PurchaseOrderId = purchaseOrderId,
            PONumber = poNumber,
            TenantId = tenantId,
            InvoiceDate = invoiceDate.Date,
            DueDate = dueDate.Date,
            CurrencyCode = currencyCode.ToUpperInvariant(),
            TaxAmount = taxAmount,
            CreatedAt = DateTime.UtcNow
        };
        foreach (var (code, quantity, price) in lines)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Every invoice line needs a product code.");
            if (quantity <= 0 || price < 0) throw new ArgumentException($"Line {code}: quantity must be positive and the price cannot be negative.");
            invoice._lines.Add(SupplierInvoiceLine.Create(invoice.Id, tenantId, code.Trim(), quantity, price));
        }
        if (invoice._lines.Count == 0) throw new ArgumentException("An invoice needs at least one line.");
        invoice.NetAmount = Math.Round(invoice._lines.Sum(l => l.Quantity * l.UnitPrice), 2);
        return invoice;
    }

    /// <summary>Stores the outcome of a match; a matched invoice may then be approved, an invoice on hold only with the variance permission.</summary>
    public void ApplyMatch(InvoiceMatchOutcome outcome)
    {
        if (Status is SupplierInvoiceStatus.Approved or SupplierInvoiceStatus.Rejected)
            throw new InvalidOperationException($"An invoice that is {Status} cannot be matched again.");

        _matchLines.Clear();
        foreach (var line in outcome.Lines)
            _matchLines.Add(SupplierInvoiceMatchLine.Create(Id, TenantId, line));

        ReceivedValue = outcome.ReceivedValue;
        HoldReasons = outcome.IsMatched ? null : string.Join("; ", outcome.Reasons);
        Status = outcome.IsMatched ? SupplierInvoiceStatus.Matched : SupplierInvoiceStatus.OnHold;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Approves for payment. A matched invoice needs no reason; one on hold needs <paramref name="overrideVariance"/> (the caller checked
    /// the variance permission) and a written reason. Publishes <see cref="SupplierInvoiceApprovedDomainEvent"/>: Accounting books the bill.
    /// </summary>
    public void Approve(string approvedBy, bool overrideVariance, string? reason)
    {
        switch (Status)
        {
            case SupplierInvoiceStatus.Matched:
                break;
            case SupplierInvoiceStatus.OnHold:
                if (!overrideVariance)
                    throw new InvalidOperationException("The invoice is on hold: " + HoldReasons + ". Approving it needs the permission to approve a variance.");
                if (string.IsNullOrWhiteSpace(reason))
                    throw new InvalidOperationException("A reason is required to approve an invoice with a variance.");
                ApprovalReason = reason.Trim();
                break;
            default:
                throw new InvalidOperationException($"An invoice that is {Status} cannot be approved; match it first.");
        }

        Status = SupplierInvoiceStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new SupplierInvoiceApprovedDomainEvent(
            Id, InvoiceNumber, SupplierId, PurchaseOrderId, PONumber, InvoiceDate, DueDate, CurrencyCode, NetAmount, TaxAmount, ReceivedValue));
    }

    public void Reject(string reason)
    {
        if (Status == SupplierInvoiceStatus.Approved)
            throw new InvalidOperationException("An approved invoice already became a bill and cannot be rejected.");
        if (Status == SupplierInvoiceStatus.Rejected)
            throw new InvalidOperationException("The invoice is already rejected.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required to reject an invoice.", nameof(reason));

        Status = SupplierInvoiceStatus.Rejected;
        RejectionReason = reason.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}

public class SupplierInvoiceLine : BaseEntity
{
    public Guid InvoiceId { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    private SupplierInvoiceLine() { }

    internal static SupplierInvoiceLine Create(Guid invoiceId, Guid tenantId, string productCode, decimal quantity, decimal unitPrice) =>
        new() { InvoiceId = invoiceId, TenantId = tenantId, ProductCode = productCode, Quantity = quantity, UnitPrice = unitPrice, CreatedAt = DateTime.UtcNow };
}

/// <summary>What the match found for one invoice line, kept so the screen can show it and an auditor can see why an invoice was held.</summary>
public class SupplierInvoiceMatchLine : BaseEntity
{
    public Guid InvoiceId { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    public decimal Ordered { get; private set; }
    public decimal Received { get; private set; }
    public decimal Returned { get; private set; }
    public decimal PreviouslyInvoiced { get; private set; }
    public decimal Invoiced { get; private set; }
    public decimal OrderPrice { get; private set; }
    public decimal InvoicedPrice { get; private set; }
    public decimal PriceVariancePercent { get; private set; }
    public decimal QuantityVariance { get; private set; }
    public string Verdict { get; private set; } = string.Empty;

    private SupplierInvoiceMatchLine() { }

    internal static SupplierInvoiceMatchLine Create(Guid invoiceId, Guid tenantId, InvoiceMatchLine l) => new()
    {
        InvoiceId = invoiceId, TenantId = tenantId, ProductCode = l.ProductCode, Ordered = l.Ordered, Received = l.Received, Returned = l.Returned,
        PreviouslyInvoiced = l.PreviouslyInvoiced, Invoiced = l.Invoiced, OrderPrice = l.OrderPrice, InvoicedPrice = l.InvoicedPrice,
        PriceVariancePercent = l.PriceVariancePercent, QuantityVariance = l.QuantityVariance, Verdict = l.Verdict, CreatedAt = DateTime.UtcNow
    };
}

// ─── the matcher ───────────────────────────────────────────────────────────

public record InvoiceMatchTolerance(decimal QuantityPercent, decimal PricePercent);

/// <summary>An order line as the matcher sees it: ordered, accepted so far, returned to the supplier, and invoiced on earlier invoices.</summary>
public record InvoiceMatchOrderLine(string ProductCode, decimal Ordered, decimal Received, decimal Returned, decimal PreviouslyInvoiced, decimal UnitPrice);

public record InvoiceMatchInputLine(string ProductCode, decimal Quantity, decimal UnitPrice);

public record InvoiceMatchLine(
    string ProductCode, decimal Ordered, decimal Received, decimal Returned, decimal PreviouslyInvoiced, decimal Invoiced,
    decimal OrderPrice, decimal InvoicedPrice, decimal PriceVariancePercent, decimal QuantityVariance, string Verdict);

public record InvoiceMatchOutcome(List<InvoiceMatchLine> Lines, bool IsMatched, List<string> Reasons, decimal ReceivedValue);

/// <summary>
/// The three-way match (order, receipt, invoice) with tolerances. Pure and deterministic: the same inputs give the same outcome.
/// An invoice may bill less than what was received (the rest comes on a later invoice) but not more than received − returned −
/// already invoiced, beyond the quantity tolerance; the unit price must be within the price tolerance of the order price.
/// </summary>
public static class InvoiceMatcher
{
    public const string Matched = "Matched";
    public const string QuantityVariance = "QuantityVariance";
    public const string PriceVariance = "PriceVariance";
    public const string QuantityAndPriceVariance = "QuantityAndPriceVariance";
    public const string NotOnOrder = "NotOnOrder";

    public static InvoiceMatchOutcome Match(
        IReadOnlyCollection<InvoiceMatchOrderLine> order, IReadOnlyCollection<InvoiceMatchInputLine> invoice, InvoiceMatchTolerance tolerance)
    {
        var lines = new List<InvoiceMatchLine>();
        var reasons = new List<string>();
        decimal receivedValue = 0m;

        foreach (var input in invoice)
        {
            var po = order.FirstOrDefault(o => o.ProductCode.Equals(input.ProductCode, StringComparison.OrdinalIgnoreCase));
            if (po == null)
            {
                lines.Add(new InvoiceMatchLine(input.ProductCode, 0, 0, 0, 0, input.Quantity, 0, input.UnitPrice, 0, input.Quantity, NotOnOrder));
                reasons.Add($"{input.ProductCode} is not on the purchase order");
                continue;
            }

            var billable = Math.Max(0m, po.Received - po.Returned - po.PreviouslyInvoiced);
            var quantityVariance = input.Quantity - billable;
            var quantityLimit = billable * (1m + tolerance.QuantityPercent / 100m);
            var quantityOk = input.Quantity <= quantityLimit;

            var priceVariance = po.UnitPrice == 0m ? (input.UnitPrice == 0m ? 0m : 100m) : Math.Round((input.UnitPrice - po.UnitPrice) / po.UnitPrice * 100m, 2);
            var priceOk = Math.Abs(priceVariance) <= tolerance.PricePercent;

            var verdict = quantityOk && priceOk ? Matched
                : !quantityOk && !priceOk ? QuantityAndPriceVariance
                : !quantityOk ? QuantityVariance : PriceVariance;

            if (!quantityOk)
                reasons.Add($"{po.ProductCode}: invoiced {input.Quantity:0.##} but only {billable:0.##} received and not yet invoiced");
            if (!priceOk)
                reasons.Add($"{po.ProductCode}: price {input.UnitPrice:0.##} is {priceVariance:+0.##;-0.##}% from the order price {po.UnitPrice:0.##}");

            lines.Add(new InvoiceMatchLine(po.ProductCode, po.Ordered, po.Received, po.Returned, po.PreviouslyInvoiced, input.Quantity,
                po.UnitPrice, input.UnitPrice, priceVariance, quantityVariance, verdict));
            receivedValue += input.Quantity * po.UnitPrice;
        }

        return new InvoiceMatchOutcome(lines, reasons.Count == 0, reasons, Math.Round(receivedValue, 2));
    }
}

using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Entities;

public enum RfqStatus
{
    Draft = 0,
    Sent = 1,
    Awarded = 2,
    Cancelled = 3
}

public enum RfqInvitationState
{
    Invited = 0,
    Quoted = 1,
    Declined = 2
}

/// <summary>
/// A request for quotation (ROADMAP P1-8): the goods wanted, the suppliers asked, their quotations, and the one awarded.
/// Awarding produces a Draft purchase order at the quoted prices (done by the handler: it needs the PO aggregate).
/// </summary>
public class RequestForQuotation : BaseEntity
{
    public string RfqNumber { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public DateTime? NeededBy { get; private set; }
    public string? Notes { get; private set; }
    public RfqStatus Status { get; private set; } = RfqStatus.Draft;
    public DateTime? SentAt { get; private set; }
    public Guid? AwardedSupplierId { get; private set; }
    public Guid? AwardedPurchaseOrderId { get; private set; }

    private readonly List<RfqLine> _lines = new();
    public IReadOnlyCollection<RfqLine> Lines => _lines.AsReadOnly();

    private readonly List<RfqInvitation> _invitations = new();
    public IReadOnlyCollection<RfqInvitation> Invitations => _invitations.AsReadOnly();

    private readonly List<SupplierQuotation> _quotations = new();
    public IReadOnlyCollection<SupplierQuotation> Quotations => _quotations.AsReadOnly();

    private RequestForQuotation() { }

    public static RequestForQuotation Create(string rfqNumber, string title, Guid tenantId, DateTime? neededBy = null, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(rfqNumber)) throw new ArgumentException("An RFQ number is required.", nameof(rfqNumber));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A title is required.", nameof(title));
        return new RequestForQuotation
        {
            RfqNumber = rfqNumber.Trim(),
            Title = title.Trim(),
            TenantId = tenantId,
            NeededBy = neededBy,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };
    }

    public RfqLine AddLine(string productCode, string productName, decimal quantity, string unit)
    {
        EnsureStatus(RfqStatus.Draft, "Lines can only be changed while the RFQ is a draft.");
        if (string.IsNullOrWhiteSpace(productCode)) throw new ArgumentException("A product code is required.", nameof(productCode));
        if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        if (_lines.Any(l => l.ProductCode.Equals(productCode, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"{productCode} is already on this RFQ.");

        var line = RfqLine.Create(Id, TenantId, productCode.Trim(), productName, quantity, unit);
        _lines.Add(line);
        return line;
    }

    public void Invite(Guid supplierId)
    {
        EnsureStatus(RfqStatus.Draft, "Suppliers can only be invited while the RFQ is a draft.");
        if (supplierId == Guid.Empty) throw new ArgumentException("A supplier is required.", nameof(supplierId));
        if (_invitations.Any(i => i.SupplierId == supplierId)) return;
        _invitations.Add(RfqInvitation.Create(Id, TenantId, supplierId));
    }

    public void Send()
    {
        EnsureStatus(RfqStatus.Draft, "Only a draft RFQ can be sent.");
        if (_lines.Count == 0) throw new InvalidOperationException("Add at least one line before sending the RFQ.");
        if (_invitations.Count == 0) throw new InvalidOperationException("Invite at least one supplier before sending the RFQ.");
        Status = RfqStatus.Sent;
        SentAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// A supplier's answer: one unit price per RFQ line. A second answer from the same supplier replaces the first. The supplier must
    /// have been invited, every line needs a price, and the quotation must still be valid today.
    /// </summary>
    public SupplierQuotation RecordQuotation(
        Guid supplierId, string quoteNumber, DateTime validUntil, string currencyCode, int leadTimeDays,
        string? notes, IReadOnlyDictionary<Guid, decimal> unitPrices, DateTime now)
    {
        EnsureStatus(RfqStatus.Sent, "Quotations can only be recorded while the RFQ is sent and open.");
        var invitation = _invitations.FirstOrDefault(i => i.SupplierId == supplierId)
            ?? throw new InvalidOperationException("That supplier was not invited to this RFQ.");
        if (validUntil.Date < now.Date) throw new InvalidOperationException("The quotation is already expired.");
        if (string.IsNullOrWhiteSpace(currencyCode)) throw new ArgumentException("A currency is required.", nameof(currencyCode));

        foreach (var line in _lines)
        {
            if (!unitPrices.TryGetValue(line.Id, out var price) || price <= 0)
                throw new InvalidOperationException($"A unit price greater than zero is required for {line.ProductCode}.");
        }
        if (unitPrices.Keys.Any(k => _lines.All(l => l.Id != k)))
            throw new InvalidOperationException("A price was given for a line that is not on this RFQ.");

        var quotation = _quotations.FirstOrDefault(q => q.SupplierId == supplierId);
        if (quotation == null)
        {
            quotation = SupplierQuotation.Create(Id, TenantId, supplierId);
            _quotations.Add(quotation);
        }
        quotation.Update(quoteNumber, validUntil, currencyCode.ToUpperInvariant(), leadTimeDays, notes, unitPrices);
        invitation.State = RfqInvitationState.Quoted;
        UpdatedAt = DateTime.UtcNow;
        return quotation;
    }

    public void Decline(Guid supplierId)
    {
        EnsureStatus(RfqStatus.Sent, "Only an open RFQ can be declined by a supplier.");
        var invitation = _invitations.FirstOrDefault(i => i.SupplierId == supplierId)
            ?? throw new InvalidOperationException("That supplier was not invited to this RFQ.");
        invitation.State = RfqInvitationState.Declined;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>The comparison: per line the price of every quoted supplier with the lowest marked; per supplier the total and validity.</summary>
    public RfqComparison Compare(DateTime now)
    {
        var suppliers = _quotations
            .Select(q => new RfqComparisonSupplier(
                q.SupplierId, q.QuoteNumber, q.CurrencyCode, q.LeadTimeDays, q.ValidUntil, q.ValidUntil.Date < now.Date, q.IsWinner,
                _lines.Sum(l => q.PriceFor(l.Id) * l.Quantity)))
            .OrderBy(s => s.Total)
            .ToList();

        var lines = _lines.Select(l =>
        {
            var prices = _quotations
                .Select(q => new RfqComparisonPrice(q.SupplierId, q.PriceFor(l.Id), q.ValidUntil.Date < now.Date))
                .ToList();
            var usable = prices.Where(p => !p.Expired).ToList();
            var lowest = usable.Count > 0 ? usable.Min(p => p.UnitPrice) : (decimal?)null;
            return new RfqComparisonLine(l.Id, l.ProductCode, l.ProductName, l.Quantity, l.Unit, lowest, prices);
        }).ToList();

        return new RfqComparison(Id, RfqNumber, Status, lines, suppliers);
    }

    /// <summary>Awards the RFQ to one quoted supplier whose quotation is still valid; the others are marked lost.</summary>
    public SupplierQuotation Award(Guid supplierId, DateTime now)
    {
        EnsureStatus(RfqStatus.Sent, "Only an open RFQ can be awarded.");
        var quotation = _quotations.FirstOrDefault(q => q.SupplierId == supplierId)
            ?? throw new InvalidOperationException("That supplier has not quoted.");
        if (quotation.ValidUntil.Date < now.Date) throw new InvalidOperationException("The quotation has expired.");

        foreach (var q in _quotations) q.IsWinner = q.SupplierId == supplierId;
        Status = RfqStatus.Awarded;
        AwardedSupplierId = supplierId;
        UpdatedAt = DateTime.UtcNow;
        return quotation;
    }

    public void LinkPurchaseOrder(Guid purchaseOrderId) => AwardedPurchaseOrderId = purchaseOrderId;

    public void Cancel()
    {
        if (Status is RfqStatus.Awarded or RfqStatus.Cancelled)
            throw new InvalidOperationException($"An RFQ that is {Status} cannot be cancelled.");
        Status = RfqStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureStatus(RfqStatus expected, string message)
    {
        if (Status != expected) throw new InvalidOperationException($"{message} (current status: {Status}).");
    }
}

public class RfqLine : BaseEntity
{
    public Guid RfqId { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = string.Empty;

    private RfqLine() { }

    internal static RfqLine Create(Guid rfqId, Guid tenantId, string productCode, string productName, decimal quantity, string unit) =>
        new() { RfqId = rfqId, TenantId = tenantId, ProductCode = productCode, ProductName = productName, Quantity = quantity, Unit = unit, CreatedAt = DateTime.UtcNow };
}

public class RfqInvitation : BaseEntity
{
    public Guid RfqId { get; private set; }
    public Guid SupplierId { get; private set; }
    public RfqInvitationState State { get; set; } = RfqInvitationState.Invited;

    private RfqInvitation() { }

    internal static RfqInvitation Create(Guid rfqId, Guid tenantId, Guid supplierId) =>
        new() { RfqId = rfqId, TenantId = tenantId, SupplierId = supplierId, CreatedAt = DateTime.UtcNow };
}

public class SupplierQuotation : BaseEntity
{
    public Guid RfqId { get; private set; }
    public Guid SupplierId { get; private set; }
    public string QuoteNumber { get; private set; } = string.Empty;
    public DateTime ValidUntil { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public int LeadTimeDays { get; private set; }
    public string? Notes { get; private set; }
    public bool IsWinner { get; set; }

    private readonly List<QuotationLine> _lines = new();
    public IReadOnlyCollection<QuotationLine> Lines => _lines.AsReadOnly();

    private SupplierQuotation() { }

    internal static SupplierQuotation Create(Guid rfqId, Guid tenantId, Guid supplierId) =>
        new() { RfqId = rfqId, TenantId = tenantId, SupplierId = supplierId, CreatedAt = DateTime.UtcNow };

    internal void Update(string quoteNumber, DateTime validUntil, string currencyCode, int leadTimeDays, string? notes, IReadOnlyDictionary<Guid, decimal> prices)
    {
        QuoteNumber = quoteNumber ?? string.Empty;
        ValidUntil = validUntil.Date;
        CurrencyCode = currencyCode;
        LeadTimeDays = Math.Max(0, leadTimeDays);
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;

        foreach (var (rfqLineId, price) in prices)
        {
            var line = _lines.FirstOrDefault(l => l.RfqLineId == rfqLineId);
            if (line == null) _lines.Add(QuotationLine.Create(Id, TenantId, rfqLineId, price));
            else line.UnitPrice = price;
        }
    }

    public decimal PriceFor(Guid rfqLineId) => _lines.FirstOrDefault(l => l.RfqLineId == rfqLineId)?.UnitPrice ?? 0m;
}

public class QuotationLine : BaseEntity
{
    public Guid QuotationId { get; private set; }
    public Guid RfqLineId { get; private set; }
    public decimal UnitPrice { get; set; }

    private QuotationLine() { }

    internal static QuotationLine Create(Guid quotationId, Guid tenantId, Guid rfqLineId, decimal unitPrice) =>
        new() { QuotationId = quotationId, TenantId = tenantId, RfqLineId = rfqLineId, UnitPrice = unitPrice, CreatedAt = DateTime.UtcNow };
}

public record RfqComparisonPrice(Guid SupplierId, decimal UnitPrice, bool Expired);
public record RfqComparisonLine(Guid LineId, string ProductCode, string ProductName, decimal Quantity, string Unit, decimal? LowestPrice, List<RfqComparisonPrice> Prices);
public record RfqComparisonSupplier(Guid SupplierId, string QuoteNumber, string CurrencyCode, int LeadTimeDays, DateTime ValidUntil, bool Expired, bool IsWinner, decimal Total);
public record RfqComparison(Guid RfqId, string RfqNumber, RfqStatus Status, List<RfqComparisonLine> Lines, List<RfqComparisonSupplier> Suppliers);

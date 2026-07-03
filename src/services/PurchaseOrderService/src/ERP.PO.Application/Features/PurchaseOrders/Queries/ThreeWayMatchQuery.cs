using MediatR;

namespace ERP.PO.Application.Features.PurchaseOrders.Queries;

/// <summary>
/// Three-way match: PO (ordered) vs GRN (received, tracked on PurchaseOrderItem since
/// 2026-07-03's ReceiveGoodsCommand) vs Invoice. Invoice lines are supplied by the
/// caller (typically AccountingService) rather than looked up here — PurchaseOrderService
/// does not and should not own Invoice data (no cross-database access; AccountingDB
/// is Accounting's alone per CLAUDE.md). This is a stateless comparison, not a stored
/// match record.
/// </summary>
public class ThreeWayMatchQuery : IRequest<ThreeWayMatchResultDto>
{
    public Guid PurchaseOrderId { get; set; }
    public List<InvoiceLineInput> InvoiceLines { get; set; } = new();
}

public class InvoiceLineInput
{
    public string ProductCode { get; set; } = string.Empty;
    public decimal InvoicedQuantity { get; set; }
    public decimal InvoicedUnitPrice { get; set; }
}

public class ThreeWayMatchLineResult
{
    public string ProductCode { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal InvoicedQuantity { get; set; }
    public decimal PurchaseOrderUnitPrice { get; set; }
    public decimal InvoicedUnitPrice { get; set; }
    public bool QuantityMatches { get; set; }
    public bool PriceMatches { get; set; }
    public string Status { get; set; } = string.Empty; // Matched | QuantityMismatch | PriceMismatch | NotOnPurchaseOrder | NotYetReceived
}

public class ThreeWayMatchResultDto
{
    public Guid PurchaseOrderId { get; set; }
    public bool IsFullyMatched { get; set; }
    public List<ThreeWayMatchLineResult> Lines { get; set; } = new();
}

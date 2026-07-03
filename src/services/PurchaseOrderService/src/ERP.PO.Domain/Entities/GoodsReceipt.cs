using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Entities;

/// <summary>
/// One record per GRN transaction — the table already existed in
/// 001_CreatePurchasingDB.sql (ReceiptNumber/ReceivedDate/ReceivedBy/Notes) but had no
/// corresponding C# entity until 2026-07-03, same orphaned-schema pattern found
/// repeatedly elsewhere in this system. Distinct from PurchaseOrderItem.ReceivedQuantity
/// (the running total per line) — this is a log of each individual receiving event.
/// </summary>
public class GoodsReceipt : BaseEntity
{
    public Guid PurchaseOrderId { get; private set; }
    public string ReceiptNumber { get; private set; } = string.Empty;
    public DateTime ReceivedDate { get; private set; }
    public string ReceivedBy { get; private set; } = string.Empty;
    public string? Notes { get; private set; }

    private GoodsReceipt() { }

    public static GoodsReceipt Create(Guid purchaseOrderId, Guid tenantId, string receiptNumber, string receivedBy, string? notes = null)
    {
        if (purchaseOrderId == Guid.Empty)
            throw new ArgumentException("Purchase order ID is required", nameof(purchaseOrderId));

        if (string.IsNullOrWhiteSpace(receiptNumber))
            throw new ArgumentException("Receipt number is required", nameof(receiptNumber));

        return new GoodsReceipt
        {
            PurchaseOrderId = purchaseOrderId,
            TenantId = tenantId,
            ReceiptNumber = receiptNumber,
            ReceivedDate = DateTime.UtcNow,
            ReceivedBy = receivedBy,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };
    }
}

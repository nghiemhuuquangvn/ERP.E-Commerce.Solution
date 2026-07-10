namespace ERP.PO.Domain.Entities;

/// <summary>
/// Input to PurchaseOrder.ReceiveGoods() for a single line — splits what physically
/// arrived into accepted (passes quality inspection, counts toward fulfilling the PO
/// and toward Warehouse stock) and rejected (defective/wrong item, does not fulfill
/// the PO and is not added to sellable stock).
/// </summary>
public class GoodsReceiptLineInput
{
    public decimal QuantityAccepted { get; set; }
    public decimal QuantityRejected { get; set; }
}

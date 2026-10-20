using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Events;

/// <summary>
/// Raised by ReceiveGoodsCommand (GRN) — previously impossible since no command
/// existed to call PurchaseOrderItem's already-present MarkAsReceived()/
/// MarkAsPartiallyReceived() methods at all. Consumed by WarehouseService to
/// actually add the received quantities to stock.
/// </summary>
public class PurchaseOrderReceivedDomainEvent : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid PurchaseOrderId { get; }
    public string PONumber { get; }
    public Guid SupplierId { get; }
    public bool IsFullyReceived { get; }
    public List<ReceivedLine> ReceivedItems { get; }
    public DateTime OccurredOn { get; }

    public PurchaseOrderReceivedDomainEvent(Guid purchaseOrderId, string poNumber, Guid supplierId, bool isFullyReceived, List<ReceivedLine> receivedItems)
    {
        PurchaseOrderId = purchaseOrderId;
        PONumber = poNumber;
        SupplierId = supplierId;
        IsFullyReceived = isFullyReceived;
        ReceivedItems = receivedItems;
        OccurredOn = DateTime.UtcNow;
    }

    public class ReceivedLine
    {
        public string ProductCode { get; set; } = string.Empty;

        /// <summary>Accepted quantity only — what WarehouseService should add to
        /// sellable stock. Rejected units are tracked separately and must not be
        /// added to inventory.</summary>
        public decimal QuantityReceived { get; set; }

        /// <summary>Added 2026-07-10. No consumer reads this field yet — Warehouse
        /// only reacts to QuantityReceived (accepted) today.</summary>
        public decimal QuantityRejected { get; set; }
    }
}

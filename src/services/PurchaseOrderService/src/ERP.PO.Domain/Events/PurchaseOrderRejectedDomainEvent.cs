using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Events;

/// <summary>
/// Distinct from PurchaseOrderCancelledDomainEvent — Reject carries a mandatory
/// reason and only applies to a Submitted order awaiting approval, whereas Cancel
/// can end an order at any earlier-or-later stage without one.
/// </summary>
public class PurchaseOrderRejectedDomainEvent : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid PurchaseOrderId { get; }
    public string PONumber { get; }
    public Guid SupplierId { get; }
    public string Reason { get; }
    public string RejectedBy { get; }
    public DateTime OccurredOn { get; }

    public PurchaseOrderRejectedDomainEvent(Guid purchaseOrderId, string poNumber, Guid supplierId, string reason, string rejectedBy)
    {
        PurchaseOrderId = purchaseOrderId;
        PONumber = poNumber;
        SupplierId = supplierId;
        Reason = reason;
        RejectedBy = rejectedBy;
        OccurredOn = DateTime.UtcNow;
    }
}

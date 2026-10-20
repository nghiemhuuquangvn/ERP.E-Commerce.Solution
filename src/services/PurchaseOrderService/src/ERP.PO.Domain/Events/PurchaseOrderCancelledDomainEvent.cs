using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Events;

public class PurchaseOrderCancelledDomainEvent : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid PurchaseOrderId { get; }
    public string PONumber { get; }
    public string CancelledBy { get; }
    public DateTime OccurredOn { get; }

    public PurchaseOrderCancelledDomainEvent(Guid purchaseOrderId, string poNumber, string cancelledBy)
    {
        PurchaseOrderId = purchaseOrderId;
        PONumber = poNumber;
        CancelledBy = cancelledBy;
        OccurredOn = DateTime.UtcNow;
    }
}

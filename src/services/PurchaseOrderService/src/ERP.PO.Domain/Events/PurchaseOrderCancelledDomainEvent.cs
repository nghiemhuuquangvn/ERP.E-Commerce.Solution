using ERP.PO.Domain.Common;

namespace ERP.PO.Domain.Events;

public class PurchaseOrderCancelledDomainEvent : IDomainEvent
{
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

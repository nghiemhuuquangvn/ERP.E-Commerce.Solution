using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Events;

public class PurchaseOrderApprovedDomainEvent : IDomainEvent
{
    public Guid PurchaseOrderId { get; }
    public string PONumber { get; }
    public Guid SupplierId { get; }
    public string ApprovedBy { get; }
    public DateTime OccurredOn { get; }

    public PurchaseOrderApprovedDomainEvent(Guid purchaseOrderId, string poNumber, Guid supplierId, string approvedBy)
    {
        PurchaseOrderId = purchaseOrderId;
        PONumber = poNumber;
        SupplierId = supplierId;
        ApprovedBy = approvedBy;
        OccurredOn = DateTime.UtcNow;
    }
}

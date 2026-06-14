using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Events;

public class PurchaseOrderCreatedDomainEvent : IDomainEvent
{
    public Guid PurchaseOrderId { get; }
    public string PONumber { get; }
    public Guid SupplierId { get; }
    public DateTime OccurredOn { get; }

    public PurchaseOrderCreatedDomainEvent(Guid purchaseOrderId, string poNumber, Guid supplierId)
    {
        PurchaseOrderId = purchaseOrderId;
        PONumber = poNumber;
        SupplierId = supplierId;
        OccurredOn = DateTime.UtcNow;
    }
}

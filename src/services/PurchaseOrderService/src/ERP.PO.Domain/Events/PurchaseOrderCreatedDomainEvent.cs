using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Events;

public class PurchaseOrderCreatedDomainEvent : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

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

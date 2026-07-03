using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Events;

/// <summary>Raised when a receiving dock/date is scheduled for an approved PO — lets Warehouse plan dock/labor ahead of the actual GRN.</summary>
public class ReceiptScheduledDomainEvent : IDomainEvent
{
    public Guid PurchaseOrderId { get; }
    public string PONumber { get; }
    public DateTime ScheduledDate { get; }
    public DateTime OccurredOn { get; }

    public ReceiptScheduledDomainEvent(Guid purchaseOrderId, string poNumber, DateTime scheduledDate)
    {
        PurchaseOrderId = purchaseOrderId;
        PONumber = poNumber;
        ScheduledDate = scheduledDate;
        OccurredOn = DateTime.UtcNow;
    }
}

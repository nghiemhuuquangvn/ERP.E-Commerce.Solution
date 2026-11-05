using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Events;

/// <summary>
/// Raised when a Draft purchase order is submitted: it now waits for a manager's approval. NotificationService tells the holders of
/// <c>purchase_order:approve</c> (bell, and push when it is switched on). The event carries no user: the order does not know who pressed Submit.
/// </summary>
public class PurchaseOrderSubmittedDomainEvent : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid PurchaseOrderId { get; }
    public string PONumber { get; }
    public Guid SupplierId { get; }
    public decimal TotalAmount { get; }
    public string CurrencyCode { get; }
    public DateTime OccurredOn { get; }

    public PurchaseOrderSubmittedDomainEvent(Guid purchaseOrderId, string poNumber, Guid supplierId, decimal totalAmount, string currencyCode)
    {
        PurchaseOrderId = purchaseOrderId;
        PONumber = poNumber;
        SupplierId = supplierId;
        TotalAmount = totalAmount;
        CurrencyCode = currencyCode;
        OccurredOn = DateTime.UtcNow;
    }
}

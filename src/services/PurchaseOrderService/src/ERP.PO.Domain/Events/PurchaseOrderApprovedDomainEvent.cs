using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Events;

public class PurchaseOrderApprovedDomainEvent : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid PurchaseOrderId { get; }
    public string PONumber { get; }
    public Guid SupplierId { get; }
    public string ApprovedBy { get; }

    /// <summary>Order total at final approval — lines can't change after Draft, so this is what the supplier bill is for.</summary>
    public decimal TotalAmount { get; }
    public string CurrencyCode { get; }
    public DateTime OccurredOn { get; }

    public PurchaseOrderApprovedDomainEvent(Guid purchaseOrderId, string poNumber, Guid supplierId, string approvedBy, decimal totalAmount, string currencyCode)
    {
        PurchaseOrderId = purchaseOrderId;
        PONumber = poNumber;
        SupplierId = supplierId;
        ApprovedBy = approvedBy;
        TotalAmount = totalAmount;
        CurrencyCode = currencyCode;
        OccurredOn = DateTime.UtcNow;
    }
}

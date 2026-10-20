using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Events;

/// <summary>
/// Raised when Manager approval clears an order whose total is at/above
/// PurchaseOrder.FinanceApprovalThreshold, so it now needs a second Finance
/// approval before becoming Approved.
/// </summary>
public class PurchaseOrderPendingFinanceApprovalDomainEvent : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid PurchaseOrderId { get; }
    public string PONumber { get; }
    public Guid SupplierId { get; }
    public decimal TotalAmount { get; }
    public string ManagerApprovedBy { get; }
    public DateTime OccurredOn { get; }

    public PurchaseOrderPendingFinanceApprovalDomainEvent(
        Guid purchaseOrderId, string poNumber, Guid supplierId, decimal totalAmount, string managerApprovedBy)
    {
        PurchaseOrderId = purchaseOrderId;
        PONumber = poNumber;
        SupplierId = supplierId;
        TotalAmount = totalAmount;
        ManagerApprovedBy = managerApprovedBy;
        OccurredOn = DateTime.UtcNow;
    }
}

using ERP.PO.Domain.Enums;
using ERP.PO.Domain.Events;
using ERP.PO.Domain.ValueObjects;
using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Entities;

public class PurchaseOrder : BaseEntity
{
    public string PONumber { get; private set; } = string.Empty;
    public Guid SupplierId { get; private set; }
    public Supplier? Supplier { get; private set; }
    public PurchaseOrderStatus Status { get; private set; } = PurchaseOrderStatus.Draft;
    public DateTime OrderDate { get; private set; } = DateTime.UtcNow;
    public DateTime? ExpectedDeliveryDate { get; private set; }
    public Money TotalAmount { get; private set; } = Money.Zero;
    public string? Notes { get; private set; }
    public DateTime? ScheduledReceiptDate { get; private set; }
    public string? RejectionReason { get; private set; }

    /// <summary>
    /// Orders at or above this total require a second Finance approval after
    /// the Manager step; smaller orders go straight to Approved on Manager
    /// approval alone. Kept as a domain constant (not configurable per
    /// tenant) for this first pass of the multi-level approval workflow.
    /// </summary>
    public const decimal FinanceApprovalThreshold = 5000m;

    public string? ManagerApprovedBy { get; private set; }
    public DateTime? ManagerApprovedAt { get; private set; }
    public string? FinanceApprovedBy { get; private set; }
    public DateTime? FinanceApprovedAt { get; private set; }

    private readonly List<PurchaseOrderItem> _items = new();
    public IReadOnlyCollection<PurchaseOrderItem> Items => _items.AsReadOnly();

    private PurchaseOrder() { }

    public static PurchaseOrder Create(
        string poNumber,
        Guid supplierId,
        Guid tenantId,
        DateTime? expectedDeliveryDate = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(poNumber))
            throw new ArgumentException("PO Number is required", nameof(poNumber));

        if (supplierId == Guid.Empty)
            throw new ArgumentException("Supplier ID is required", nameof(supplierId));

        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required", nameof(tenantId));

        if (expectedDeliveryDate.HasValue && expectedDeliveryDate < DateTime.UtcNow)
            throw new ArgumentException(
                "Expected delivery date must be in the future",
                nameof(expectedDeliveryDate));

        var order = new PurchaseOrder
        {
            PONumber = poNumber,
            SupplierId = supplierId,
            TenantId = tenantId,
            ExpectedDeliveryDate = expectedDeliveryDate,
            Notes = notes,
            Status = PurchaseOrderStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        order.AddDomainEvent(new PurchaseOrderCreatedDomainEvent(order.Id, poNumber, supplierId));
        return order;
    }

    public void AddItem(PurchaseOrderItem item)
    {
        _items.Add(item);
        RecalculateTotal();
    }

    public void RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is not null)
        {
            _items.Remove(item);
            RecalculateTotal();
        }
    }

    public void Submit()
    {
        if (Status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException("Only Draft orders can be submitted.");

        if (!Items.Any())
            throw new InvalidOperationException("Cannot submit a purchase order with no items.");

        RecalculateTotal();
        Status = PurchaseOrderStatus.Submitted;
    }

    /// <summary>
    /// Step 1 of the approval chain (Manager). Orders below
    /// FinanceApprovalThreshold are fully approved here; orders at/above it
    /// move to PendingFinanceApproval and require ApproveFinance() as well.
    /// </summary>
    public void Approve(string approvedBy)
    {
        if (Status != PurchaseOrderStatus.Submitted)
            throw new InvalidOperationException("Only Submitted orders can be approved.");

        ManagerApprovedBy = approvedBy;
        ManagerApprovedAt = DateTime.UtcNow;
        UpdatedBy = approvedBy;
        UpdatedAt = DateTime.UtcNow;

        if (TotalAmount.Amount >= FinanceApprovalThreshold)
        {
            Status = PurchaseOrderStatus.PendingFinanceApproval;
            AddDomainEvent(new PurchaseOrderPendingFinanceApprovalDomainEvent(
                Id, PONumber, SupplierId, TotalAmount.Amount, approvedBy));
        }
        else
        {
            Status = PurchaseOrderStatus.Approved;
            AddDomainEvent(new PurchaseOrderApprovedDomainEvent(Id, PONumber, SupplierId, approvedBy));
        }
    }

    /// <summary>
    /// Step 2 of the approval chain (Finance) — only reachable for orders
    /// Manager-approved at/above FinanceApprovalThreshold.
    /// </summary>
    public void ApproveFinance(string approvedBy)
    {
        if (Status != PurchaseOrderStatus.PendingFinanceApproval)
            throw new InvalidOperationException("Only orders pending Finance approval can be Finance-approved.");

        FinanceApprovedBy = approvedBy;
        FinanceApprovedAt = DateTime.UtcNow;
        Status = PurchaseOrderStatus.Approved;
        UpdatedBy = approvedBy;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PurchaseOrderApprovedDomainEvent(Id, PONumber, SupplierId, approvedBy));
    }

    /// <summary>
    /// Distinct from Cancel: only valid while awaiting approval (Manager or Finance
    /// step), and requires a reason — lets the approver send an order back with
    /// feedback instead of ending it permanently. Unlike Cancel, Rejected is terminal
    /// (matches SalesOrder.Reject()'s behavior in SaleOrderService, kept consistent
    /// across the two services).
    /// </summary>
    public void Reject(string reason, string rejectedBy)
    {
        if (Status != PurchaseOrderStatus.Submitted && Status != PurchaseOrderStatus.PendingFinanceApproval)
            throw new InvalidOperationException("Only Submitted or Pending-Finance-Approval orders can be rejected.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required to reject a purchase order.", nameof(reason));

        Status = PurchaseOrderStatus.Rejected;
        RejectionReason = reason;
        UpdatedBy = rejectedBy;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PurchaseOrderRejectedDomainEvent(Id, PONumber, SupplierId, reason, rejectedBy));
    }

    public void Cancel(string cancelledBy)
    {
        if (Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Rejected)
            throw new InvalidOperationException("Order cannot be cancelled in its current state.");

        Status = PurchaseOrderStatus.Cancelled;
        UpdatedBy = cancelledBy;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PurchaseOrderCancelledDomainEvent(Id, PONumber, cancelledBy));
    }

    private void RecalculateTotal()
    {
        var total = _items.Sum(i => i.TotalPrice.Amount);
        TotalAmount = new Money(total, TotalAmount.Currency);
    }

    /// <summary>Schedules a receiving dock/date — plannable ahead of the actual GRN, once the PO is Approved (or already has a partial receipt in progress).</summary>
    public void ScheduleReceipt(DateTime scheduledDate)
    {
        if (Status != PurchaseOrderStatus.Approved && Status != PurchaseOrderStatus.Ordered && Status != PurchaseOrderStatus.PartiallyReceived)
            throw new InvalidOperationException($"Cannot schedule receipt for a purchase order in {Status} status");

        ScheduledReceiptDate = scheduledDate;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new ReceiptScheduledDomainEvent(Id, PONumber, scheduledDate));
    }

    /// <summary>
    /// Goods Receipt Note (GRN) — records quantities actually received per line.
    /// Was entirely impossible before 2026-07-03: PurchaseOrderItem.MarkAsReceived()/
    /// MarkAsPartiallyReceived() already existed but nothing in the Application layer
    /// ever called them, and PurchaseOrderStatus.Received/PartiallyReceived were
    /// unreachable enum values.
    /// </summary>
    public PurchaseOrderReceivedDomainEvent ReceiveGoods(IReadOnlyDictionary<Guid, GoodsReceiptLineInput> receivedQuantities)
    {
        if (Status != PurchaseOrderStatus.Approved && Status != PurchaseOrderStatus.Ordered && Status != PurchaseOrderStatus.PartiallyReceived)
            throw new InvalidOperationException($"Cannot receive goods for a purchase order in {Status} status");

        if (receivedQuantities.Count == 0)
            throw new ArgumentException("At least one line item quantity is required", nameof(receivedQuantities));

        var receivedLines = new List<PurchaseOrderReceivedDomainEvent.ReceivedLine>();

        foreach (var (itemId, line) in receivedQuantities)
        {
            var item = _items.FirstOrDefault(i => i.Id == itemId)
                ?? throw new InvalidOperationException($"Item {itemId} is not on this purchase order");

            item.ReceiveQuantity(line.QuantityAccepted, line.QuantityRejected);
            receivedLines.Add(new PurchaseOrderReceivedDomainEvent.ReceivedLine
            {
                ProductCode = item.ProductCode,
                QuantityReceived = line.QuantityAccepted,
                QuantityRejected = line.QuantityRejected
            });
        }

        var fullyReceived = _items.All(i => i.RemainingQuantity == 0);
        Status = fullyReceived ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
        UpdatedAt = DateTime.UtcNow;

        var receivedEvent = new PurchaseOrderReceivedDomainEvent(Id, PONumber, SupplierId, fullyReceived, receivedLines);
        AddDomainEvent(receivedEvent);
        return receivedEvent;
    }
}

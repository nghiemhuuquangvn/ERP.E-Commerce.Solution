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

    public void Approve(string approvedBy)
    {
        if (Status != PurchaseOrderStatus.Submitted)
            throw new InvalidOperationException("Only Submitted orders can be approved.");

        Status = PurchaseOrderStatus.Approved;
        UpdatedBy = approvedBy;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PurchaseOrderApprovedDomainEvent(Id, PONumber, SupplierId, approvedBy));
    }

    public void Cancel(string cancelledBy)
    {
        if (Status == PurchaseOrderStatus.Received || Status == PurchaseOrderStatus.Cancelled)
            throw new InvalidOperationException("Order cannot be cancelled in its current state.");

        Status = PurchaseOrderStatus.Cancelled;
        UpdatedBy = cancelledBy;
        UpdatedAt = DateTime.UtcNow;
    }

    private void RecalculateTotal()
    {
        var total = _items.Sum(i => i.TotalPrice.Amount);
        TotalAmount = new Money(total, TotalAmount.Currency);
    }
}

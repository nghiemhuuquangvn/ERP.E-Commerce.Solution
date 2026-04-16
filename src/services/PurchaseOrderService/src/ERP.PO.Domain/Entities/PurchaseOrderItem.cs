using ERP.PO.Domain.Common;
using ERP.PO.Domain.Enums;
using ERP.PO.Domain.ValueObjects;

namespace ERP.PO.Domain.Entities;

public class PurchaseOrderItem : BaseEntity
{
    public Guid PurchaseOrderId { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = string.Empty;
    public Money UnitPrice { get; private set; } = Money.Zero;
    public Money TotalPrice => new(Quantity * UnitPrice.Amount, UnitPrice.Currency);
    public PurchaseOrderItemStatus Status { get; private set; } = PurchaseOrderItemStatus.Pending;

    private PurchaseOrderItem() { }

    public static PurchaseOrderItem Create(
        Guid purchaseOrderId,
        string productCode,
        string productName,
        decimal quantity,
        string unit,
        Money unitPrice)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        return new PurchaseOrderItem
        {
            PurchaseOrderId = purchaseOrderId,
            ProductCode = productCode,
            ProductName = productName,
            Quantity = quantity,
            Unit = unit,
            UnitPrice = unitPrice,
            Status = PurchaseOrderItemStatus.Pending
        };
    }

    public void UpdateQuantity(decimal quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        Quantity = quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsReceived()
    {
        Status = PurchaseOrderItemStatus.Received;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsPartiallyReceived()
    {
        Status = PurchaseOrderItemStatus.PartiallyReceived;
        UpdatedAt = DateTime.UtcNow;
    }
}

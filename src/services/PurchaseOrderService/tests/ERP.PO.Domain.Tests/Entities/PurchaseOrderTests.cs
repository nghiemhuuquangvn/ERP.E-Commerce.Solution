using Xunit;
using ERP.PO.Domain.Entities;
using ERP.PO.Domain.Enums;
using ERP.PO.Domain.ValueObjects;

namespace ERP.PO.Domain.Tests.Entities;

public class PurchaseOrderTests
{
    [Fact]
    public void Create_ValidParameters_CreatesOrderInDraftStatus()
    {
        var poNumber = "PO-2024-001";
        var supplierId = Guid.NewGuid();

        var order = PurchaseOrder.Create(poNumber, supplierId);

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(poNumber, order.PONumber);
        Assert.Equal(supplierId, order.SupplierId);
        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
    }

    [Fact]
    public void Create_RaisesCreatedDomainEvent()
    {
        var poNumber = "PO-2024-002";
        var supplierId = Guid.NewGuid();

        var order = PurchaseOrder.Create(poNumber, supplierId);

        Assert.NotEmpty(order.GetDomainEvents());
    }

    [Fact]
    public void AddItem_ValidItem_AddsItemToOrder()
    {
        var order = PurchaseOrder.Create("PO-2024-003", Guid.NewGuid());
        var item = PurchaseOrderItem.Create(
            order.Id,
            "PROD-001",
            "Product 1",
            10,
            "PCS",
            new Money(100, "USD"));

        order.AddItem(item);

        Assert.Single(order.Items);
    }

    [Fact]
    public void AddItem_RecalculatesTotal()
    {
        var order = PurchaseOrder.Create("PO-2024-004", Guid.NewGuid());
        var item1 = PurchaseOrderItem.Create(order.Id, "PROD-001", "Product 1", 10, "PCS", new Money(100, "USD"));
        var item2 = PurchaseOrderItem.Create(order.Id, "PROD-002", "Product 2", 5, "PCS", new Money(200, "USD"));

        order.AddItem(item1);
        order.AddItem(item2);

        Assert.Equal(2000, order.TotalAmount.Amount);
    }

    [Fact]
    public void RemoveItem_ValidItemId_RemovesItem()
    {
        var order = PurchaseOrder.Create("PO-2024-005", Guid.NewGuid());
        var item = PurchaseOrderItem.Create(order.Id, "PROD-001", "Product 1", 10, "PCS", new Money(100, "USD"));
        order.AddItem(item);

        order.RemoveItem(item.Id);

        Assert.Empty(order.Items);
    }

    [Fact]
    public void Submit_DraftOrder_ChangesStatusToSubmitted()
    {
        var order = PurchaseOrder.Create("PO-2024-006", Guid.NewGuid());

        order.Submit();

        Assert.Equal(PurchaseOrderStatus.Submitted, order.Status);
    }

    [Fact]
    public void Submit_NonDraftOrder_ThrowsException()
    {
        var order = PurchaseOrder.Create("PO-2024-007", Guid.NewGuid());
        order.Submit();

        Assert.Throws<InvalidOperationException>(() => order.Submit());
    }

    [Fact]
    public void Approve_SubmittedOrder_ChangesStatusToApproved()
    {
        var order = PurchaseOrder.Create("PO-2024-008", Guid.NewGuid());
        order.Submit();

        order.Approve("approver@example.com");

        Assert.Equal(PurchaseOrderStatus.Approved, order.Status);
    }

    [Fact]
    public void Approve_NonSubmittedOrder_ThrowsException()
    {
        var order = PurchaseOrder.Create("PO-2024-009", Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => order.Approve("approver@example.com"));
    }

    [Fact]
    public void Cancel_SubmittedOrder_ChangesStatusToCancelled()
    {
        var order = PurchaseOrder.Create("PO-2024-010", Guid.NewGuid());
        order.Submit();

        order.Cancel("canceller@example.com");

        Assert.Equal(PurchaseOrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_ReceivedOrder_ThrowsException()
    {
        var order = PurchaseOrder.Create("PO-2024-011", Guid.NewGuid());
        var item = PurchaseOrderItem.Create(order.Id, "PROD-001", "Product 1", 10, "PCS", new Money(100, "USD"));
        order.AddItem(item);
        order.Submit();
        order.Approve("approver@example.com");

        Assert.Throws<InvalidOperationException>(() => order.Cancel("canceller@example.com"));
    }
}

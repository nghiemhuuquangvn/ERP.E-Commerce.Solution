using Xunit;
using ERP.PO.Domain.Entities;
using ERP.PO.Domain.Enums;
using ERP.PO.Domain.ValueObjects;

namespace ERP.PO.Domain.Tests.Entities;

public class PurchaseOrderItemTests
{
    [Fact]
    public void Create_ValidParameters_CreatesItemWithPendingStatus()
    {
        var purchaseOrderId = Guid.NewGuid();
        var productCode = "PROD-001";
        var productName = "Product 1";
        var quantity = 10m;
        var unit = "PCS";
        var unitPrice = new Money(100, "USD");

        var item = PurchaseOrderItem.Create(purchaseOrderId, productCode, productName, quantity, unit, unitPrice);

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal(purchaseOrderId, item.PurchaseOrderId);
        Assert.Equal(productCode, item.ProductCode);
        Assert.Equal(productName, item.ProductName);
        Assert.Equal(quantity, item.Quantity);
        Assert.Equal(unit, item.Unit);
        Assert.Equal(PurchaseOrderItemStatus.Pending, item.Status);
    }

    [Fact]
    public void Create_ZeroQuantity_ThrowsArgumentException()
    {
        var purchaseOrderId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            PurchaseOrderItem.Create(purchaseOrderId, "PROD-002", "Product 2", 0, "PCS", new Money(100, "USD")));
    }

    [Fact]
    public void Create_NegativeQuantity_ThrowsArgumentException()
    {
        var purchaseOrderId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            PurchaseOrderItem.Create(purchaseOrderId, "PROD-003", "Product 3", -5, "PCS", new Money(100, "USD")));
    }

    [Fact]
    public void TotalPrice_CalculatesCorrectly()
    {
        var item = PurchaseOrderItem.Create(
            Guid.NewGuid(),
            "PROD-004",
            "Product 4",
            10,
            "PCS",
            new Money(50.50m, "USD"));

        Assert.Equal(505, item.TotalPrice.Amount);
    }

    [Fact]
    public void UpdateQuantity_ValidQuantity_UpdatesQuantity()
    {
        var item = PurchaseOrderItem.Create(
            Guid.NewGuid(),
            "PROD-005",
            "Product 5",
            10,
            "PCS",
            new Money(100, "USD"));

        item.UpdateQuantity(20);

        Assert.Equal(20, item.Quantity);
    }

    [Fact]
    public void UpdateQuantity_ZeroQuantity_ThrowsArgumentException()
    {
        var item = PurchaseOrderItem.Create(
            Guid.NewGuid(),
            "PROD-006",
            "Product 6",
            10,
            "PCS",
            new Money(100, "USD"));

        Assert.Throws<ArgumentException>(() => item.UpdateQuantity(0));
    }

    [Fact]
    public void MarkAsReceived_ChangesStatusToReceived()
    {
        var item = PurchaseOrderItem.Create(
            Guid.NewGuid(),
            "PROD-007",
            "Product 7",
            10,
            "PCS",
            new Money(100, "USD"));

        item.MarkAsReceived();

        Assert.Equal(PurchaseOrderItemStatus.Received, item.Status);
    }

    [Fact]
    public void MarkAsPartiallyReceived_ChangesStatusToPartiallyReceived()
    {
        var item = PurchaseOrderItem.Create(
            Guid.NewGuid(),
            "PROD-008",
            "Product 8",
            10,
            "PCS",
            new Money(100, "USD"));

        item.MarkAsPartiallyReceived();

        Assert.Equal(PurchaseOrderItemStatus.PartiallyReceived, item.Status);
    }
}

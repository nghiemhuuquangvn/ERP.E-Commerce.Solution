using ERP.PO.Domain.Entities;
using ERP.PO.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ERP.PO.UnitTests.PurchaseOrders;

public class GoodsReceiptEventTests
{
    [Fact]
    public void Received_lines_carry_the_po_unit_price_and_the_goods_receipt_id()
    {
        var order = PurchaseOrder.Create("PO-1", Guid.NewGuid(), Guid.NewGuid());
        var item = PurchaseOrderItem.Create(order.Id, "SKU-1", "Item", 10, "pcs", new Money(50m, "VND"));
        order.AddItem(item);
        order.Submit();
        order.Approve("manager");
        var receiptId = Guid.NewGuid();

        var evt = order.ReceiveGoods(
            new Dictionary<Guid, GoodsReceiptLineInput> { [item.Id] = new() { QuantityAccepted = 4, QuantityRejected = 1 } },
            receiptId);

        var line = evt.ReceivedItems.Single();
        line.UnitPrice.Should().Be(50m);
        line.CurrencyCode.Should().Be("VND");
        line.GoodsReceiptId.Should().Be(receiptId);
        line.QuantityReceived.Should().Be(4);
    }
}

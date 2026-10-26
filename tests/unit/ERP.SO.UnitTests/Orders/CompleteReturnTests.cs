using ERP.SO.Domain.Entities;
using ERP.SO.Domain.Events;
using ERP.SO.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ERP.SO.UnitTests.Orders;

/// <summary>ROADMAP P1-1e: the refund amount of a return is what the ledger credits, so it is checked against the order.</summary>
public class CompleteReturnTests
{
    private static SalesOrder ReturnRequestedOrder()
    {
        var order = SalesOrder.Create(Guid.NewGuid(), "Customer", "c@example.com", new Address("1 Street", "City", "State", "700000", "VN"),
            new List<OrderLineItem> { new(Guid.NewGuid(), "Ao thun", 2, 500m) });
        order.Submit();
        order.Approve();
        order.MarkAsPicked();
        order.Ship("TRACK-1", "DHL");
        order.MarkAsDelivered();
        order.RequestReturn("wrong size");
        return order;
    }

    [Fact]
    public void A_refund_up_to_the_order_total_completes_the_return_and_raises_the_event()
    {
        var order = ReturnRequestedOrder();

        order.CompleteReturn(order.TotalAmount);

        var evt = order.DomainEvents.OfType<SalesOrderReturnedEvent>().Single();
        evt.OrderId.Should().Be(order.Id);
        evt.RefundAmount.Should().Be(order.TotalAmount);
    }

    [Fact]
    public void The_event_lists_the_shipped_lines_so_Warehouse_can_restock_them()
    {
        var order = ReturnRequestedOrder();

        order.CompleteReturn(order.TotalAmount);

        var evt = order.DomainEvents.OfType<SalesOrderReturnedEvent>().Single();
        evt.Lines.Should().ContainSingle().Which.Quantity.Should().Be(2);
        evt.Lines[0].ProductId.Should().Be(order.LineItems.Single().ProductId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1000.01)]
    public void A_refund_that_is_not_positive_or_above_the_order_total_is_refused(double refund)
    {
        var order = ReturnRequestedOrder();

        var act = () => order.CompleteReturn((decimal)refund);

        act.Should().Throw<InvalidOperationException>();
        order.DomainEvents.OfType<SalesOrderReturnedEvent>().Should().BeEmpty();
    }
}

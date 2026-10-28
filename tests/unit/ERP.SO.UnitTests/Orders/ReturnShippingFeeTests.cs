using ERP.SO.Domain.Entities;
using ERP.SO.Domain.Events;
using ERP.SO.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ERP.SO.UnitTests.Orders;

/// <summary>ROADMAP P1-11c: a return refunds the goods; the delivery fee comes back only when the approver ticks it.</summary>
public class ReturnShippingFeeTests
{
    // goods 1 000, delivery 30, total 1 030
    private static SalesOrder ReturnRequestedOrder(decimal fee = 30m)
    {
        var order = SalesOrder.Create(Guid.NewGuid(), "Customer", "c@example.com", new Address("1 Street", "City", "State", "700000", "VN"),
            new List<OrderLineItem> { new(Guid.NewGuid(), "Ao thun", 2, 500m) });
        order.SetShippingFee(fee);
        order.Submit();
        order.Approve();
        order.MarkAsPicked();
        order.Ship("TRACK-1", "DHL");
        order.MarkAsDelivered();
        order.RequestReturn("wrong size");
        return order;
    }

    [Fact]
    public void Without_the_tick_the_goods_are_refunded_and_the_fee_stays_with_the_shop()
    {
        var order = ReturnRequestedOrder();

        order.CompleteReturn(1000m);

        var evt = order.DomainEvents.OfType<SalesOrderReturnedEvent>().Single();
        (evt.RefundAmount, evt.ShippingFeeRefunded, order.ReturnShippingFeeRefunded).Should().Be((1000m, false, false));
    }

    [Fact]
    public void Without_the_tick_a_refund_above_the_goods_is_refused()
    {
        var order = ReturnRequestedOrder();

        var act = () => order.CompleteReturn(1030m);       // the order total, which is what the admin screen used to send

        act.Should().Throw<InvalidOperationException>().WithMessage("*delivery fee*ticked*");
        order.DomainEvents.OfType<SalesOrderReturnedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void With_the_tick_the_goods_and_the_fee_can_be_refunded()
    {
        var order = ReturnRequestedOrder();

        order.CompleteReturn(1030m, refundShippingFee: true);

        var evt = order.DomainEvents.OfType<SalesOrderReturnedEvent>().Single();
        (evt.RefundAmount, evt.ShippingFeeRefunded, order.ReturnShippingFeeRefunded).Should().Be((1030m, true, true));
    }

    [Fact]
    public void With_the_tick_a_refund_above_goods_plus_fee_is_still_refused()
    {
        var order = ReturnRequestedOrder();

        var act = () => order.CompleteReturn(1030.01m, refundShippingFee: true);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void The_tick_can_be_given_with_a_partial_refund_and_is_recorded_as_given()
    {
        var order = ReturnRequestedOrder();

        order.CompleteReturn(800m, refundShippingFee: true);

        order.RefundAmount.Should().Be(800m);
        order.ReturnShippingFeeRefunded.Should().BeTrue();
    }

    [Fact]
    public void An_order_with_free_delivery_never_records_a_fee_refund()
    {
        var order = ReturnRequestedOrder(fee: 0m);

        order.CompleteReturn(1000m, refundShippingFee: true);

        order.ReturnShippingFeeRefunded.Should().BeFalse("there was no fee to give back");
    }

    [Fact]
    public void A_coupon_lowers_the_goods_the_refund_is_capped_at()
    {
        var order = SalesOrder.Create(Guid.NewGuid(), "Customer", "c@example.com", new Address("1 Street", "City", "State", "700000", "VN"),
            new List<OrderLineItem> { new(Guid.NewGuid(), "Ao thun", 2, 500m) });
        order.ApplyDiscount(Guid.NewGuid(), 100m);
        order.SetShippingFee(30m);                                   // paid 930 = goods 900 + fee 30
        order.Submit(); order.Approve(); order.MarkAsPicked(); order.Ship("T", "DHL"); order.MarkAsDelivered(); order.RequestReturn("x");

        var tooMuch = () => order.CompleteReturn(901m);

        tooMuch.Should().Throw<InvalidOperationException>();
        order.CompleteReturn(900m);
        order.RefundAmount.Should().Be(900m);
    }
}

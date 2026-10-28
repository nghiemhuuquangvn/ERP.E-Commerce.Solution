using ERP.Shared.Messaging;
using ERP.SO.Application.Commands;
using ERP.SO.Application.Handlers;
using ERP.SO.Application.Interfaces;
using ERP.SO.Application.Sagas;
using ERP.SO.Domain.Entities;
using ERP.SO.Domain.ValueObjects;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.SO.UnitTests.Orders;

/// <summary>ROADMAP P1-11d: the goods of a return come back through a carrier before the refund; an order in several parcels is delivered with its last parcel.</summary>
public class ReturnPickupTests
{
    private readonly Mock<ISalesOrderRepository> _orders = new();
    private readonly Mock<IShippingServiceClient> _shipping = new();

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

    private ApproveReturnCommandHandler Approver(SalesOrder order)
    {
        _orders.Setup(o => o.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return new ApproveReturnCommandHandler(_orders.Object, _shipping.Object);
    }

    [Fact]
    public async Task With_a_booked_pickup_the_refund_waits_for_the_goods()
    {
        var order = ReturnRequestedOrder();
        _shipping.Setup(s => s.BookReturnAsync(order.Id, "wrong size", It.IsAny<CancellationToken>())).ReturnsAsync(new ReturnPickupResult(1, 1, null));

        var dto = await Approver(order).Handle(new ApproveReturnCommand { OrderId = order.Id }, default);

        dto.ReturnPickupBooked.Should().BeTrue();
        dto.ReturnApprovedAt.Should().NotBeNull();
        var early = () => order.CompleteReturn(1000m);
        early.Should().Throw<InvalidOperationException>().WithMessage("*still on their way*");

        order.MarkReturnGoodsReceived();
        order.MarkReturnGoodsReceived();                     // a repeated message changes nothing
        order.CompleteReturn(1000m);
        order.Status.Should().Be(OrderStatus.Returned);
    }

    [Fact]
    public async Task When_no_carrier_took_the_pickup_the_return_goes_on_by_hand()
    {
        var order = ReturnRequestedOrder();
        _shipping.Setup(s => s.BookReturnAsync(order.Id, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ReturnPickupResult(1, 0, "ghtk: address not served"));

        var dto = await Approver(order).Handle(new ApproveReturnCommand { OrderId = order.Id }, default);

        dto.ReturnPickupBooked.Should().BeFalse();
        dto.ReturnPickupNote.Should().Contain("address not served");
        order.CompleteReturn(1000m);                        // same as before P1-11d
        order.Status.Should().Be(OrderStatus.Returned);
    }

    [Fact]
    public async Task When_shipping_cannot_be_reached_the_return_is_not_approved()
    {
        var order = ReturnRequestedOrder();
        _shipping.Setup(s => s.BookReturnAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((ReturnPickupResult?)null);

        var act = () => Approver(order).Handle(new ApproveReturnCommand { OrderId = order.Id }, default);

        await act.Should().ThrowAsync<ERP.Shared.Infrastructure.Exceptions.BusinessRuleException>();
        order.ReturnApprovedAt.Should().BeNull();
    }

    [Fact]
    public async Task Approving_again_after_a_refused_booking_can_still_book_the_pickup_but_never_unbooks_it()
    {
        var order = ReturnRequestedOrder();
        _shipping.SetupSequence(s => s.BookReturnAsync(order.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReturnPickupResult(1, 0, "refused"))
            .ReturnsAsync(new ReturnPickupResult(1, 1, null))
            .ReturnsAsync(new ReturnPickupResult(1, 0, "odd answer"));
        var handler = Approver(order);

        await handler.Handle(new ApproveReturnCommand { OrderId = order.Id }, default);
        order.ReturnPickupBooked.Should().BeFalse();
        await handler.Handle(new ApproveReturnCommand { OrderId = order.Id }, default);
        order.ReturnPickupBooked.Should().BeTrue();
        await handler.Handle(new ApproveReturnCommand { OrderId = order.Id }, default);
        order.ReturnPickupBooked.Should().BeTrue();
    }

    [Fact]
    public void Receiving_goods_for_a_return_with_no_booked_pickup_changes_nothing()
    {
        var order = ReturnRequestedOrder();
        order.MarkReturnGoodsReceived();
        order.ReturnGoodsReceivedAt.Should().BeNull();
    }

    // ---- several parcels ----

    [Fact]
    public async Task An_order_with_parcels_still_on_the_way_is_not_delivered_yet()
    {
        var order = SalesOrder.Create(Guid.NewGuid(), "C", "c@example.com", new Address("1", "C", "S", "7", "VN"), new List<OrderLineItem> { new(Guid.NewGuid(), "Ao", 2, 500m) });
        order.Submit(); order.Approve(); order.MarkAsPicked(); order.Ship("T", "DHL");
        _orders.Setup(o => o.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var mediator = new Mock<IMediator>();
        var uow = new Mock<ISalesOrderUnitOfWork>();
        uow.SetupGet(u => u.Orders).Returns(_orders.Object);
        var saga = new OrderFulfillmentSaga(uow.Object, Mock.Of<IMessageBus>(), mediator.Object, NullLogger<OrderFulfillmentSaga>.Instance);

        (await saga.HandleDeliveryConfirmedAsync(order.Id, otherParcelsPending: 1, default)).Should().BeFalse();
        mediator.VerifyNoOtherCalls();

        (await saga.HandleDeliveryConfirmedAsync(order.Id, otherParcelsPending: 0, default)).Should().BeTrue();
        mediator.Verify(m => m.Send(It.IsAny<MarkAsDeliveredCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

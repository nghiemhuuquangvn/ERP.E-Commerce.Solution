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

namespace ERP.SO.UnitTests.Sagas;

/// <summary>ROADMAP P1-5: the carrier's DeliveryConfirmed turns a Shipped order into Delivered, and nothing else.</summary>
public class DeliveryConfirmedTests
{
    private readonly Mock<ISalesOrderUnitOfWork> _uow = new();
    private readonly Mock<ISalesOrderRepository> _orders = new();
    private readonly Mock<IMediator> _mediator = new();

    public DeliveryConfirmedTests() => _uow.SetupGet(u => u.Orders).Returns(_orders.Object);

    private OrderFulfillmentSaga Saga() => new(_uow.Object, Mock.Of<IMessageBus>(), _mediator.Object, NullLogger<OrderFulfillmentSaga>.Instance);

    private SalesOrder ShippedOrder()
    {
        var order = SalesOrder.Create(Guid.NewGuid(), "Customer", "c@example.com", new Address("1 Street", "City", "State", "700000", "VN"),
            new List<OrderLineItem> { new(Guid.NewGuid(), "Ao thun", 2, 500m) });
        order.Submit();
        order.Approve();
        order.MarkAsPicked();
        order.Ship("TRACK-1", "DHL");
        _orders.Setup(o => o.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return order;
    }

    [Fact]
    public async Task A_shipped_order_is_marked_delivered_with_the_carriers_confirmation_as_the_reason()
    {
        var order = ShippedOrder();

        var changed = await Saga().HandleDeliveryConfirmedAsync(order.Id, default);

        changed.Should().BeTrue();
        _mediator.Verify(m => m.Send(It.Is<MarkAsDeliveredCommand>(c => c.OrderId == order.Id && c.Reason == "Delivered (carrier confirmation)"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task An_order_that_is_already_delivered_or_not_shipped_is_left_alone()
    {
        var delivered = ShippedOrder();
        delivered.MarkAsDelivered();
        (await Saga().HandleDeliveryConfirmedAsync(delivered.Id, default)).Should().BeFalse();

        var draft = SalesOrder.Create(Guid.NewGuid(), "C", "c@example.com", new Address("1", "C", "S", "7", "VN"),
            new List<OrderLineItem> { new(Guid.NewGuid(), "Ao", 1, 10m) });
        _orders.Setup(o => o.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draft);
        (await Saga().HandleDeliveryConfirmedAsync(draft.Id, default)).Should().BeFalse();

        _mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task An_unknown_order_is_ignored()
    {
        (await Saga().HandleDeliveryConfirmedAsync(Guid.NewGuid(), default)).Should().BeFalse();
        _mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public void The_reason_reaches_the_status_history()
    {
        var order = ShippedOrder();

        order.MarkAsDelivered("Delivered (carrier confirmation)");

        order.StatusHistory.Last().Reason.Should().Be("Delivered (carrier confirmation)");
    }
}

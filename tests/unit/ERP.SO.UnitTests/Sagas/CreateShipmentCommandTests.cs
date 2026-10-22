using ERP.Shared.Messaging;
using ERP.SO.Application.Handlers;
using ERP.SO.Application.Interfaces;
using ERP.SO.Application.Sagas;
using ERP.SO.Domain.Entities;
using ERP.SO.Domain.Sagas;
using ERP.SO.Domain.ValueObjects;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.SO.UnitTests.Sagas;

public class CreateShipmentCommandTests
{
    private readonly Mock<ISalesOrderUnitOfWork> _uow = new();
    private readonly Mock<ISalesOrderRepository> _orders = new();
    private readonly Mock<IOrderFulfillmentSagaRepository> _sagas = new();
    private readonly Mock<IMessageBus> _bus = new();

    public CreateShipmentCommandTests()
    {
        _uow.SetupGet(u => u.Orders).Returns(_orders.Object);
        _uow.SetupGet(u => u.Sagas).Returns(_sagas.Object);
    }

    private OrderFulfillmentSaga Saga() =>
        new(_uow.Object, _bus.Object, Mock.Of<IMediator>(), NullLogger<OrderFulfillmentSaga>.Instance);

    private OrderFulfillmentSagaState ApprovedSagaAwaitingInventory(SalesOrder order)
    {
        var state = OrderFulfillmentSagaState.Start(order.Id, Guid.NewGuid());
        state.PaymentInitiated(Guid.NewGuid());
        state.MarkApproved();
        _sagas.Setup(s => s.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(state);
        return state;
    }

    [Fact]
    public async Task Shipment_request_carries_recipient_address_and_lines()
    {
        var productId = Guid.NewGuid();
        var order = SalesOrder.Create(
            Guid.NewGuid(), "Nguyen Van A", "a@example.com",
            new Address("12 Le Loi", "Quan 1", "Ho Chi Minh", "700000", "Vietnam"),
            new List<OrderLineItem> { new() { ProductId = productId, ProductName = "Ao thun", Quantity = 3, UnitPrice = 100m } });
        _orders.Setup(o => o.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var state = ApprovedSagaAwaitingInventory(order);

        await Saga().HandleInventoryReservedAsync(order.Id, Guid.NewGuid(), default);

        _bus.Verify(b => b.PublishAsync(It.Is<CreateShipmentCommand>(c =>
            c.OrderId == order.Id &&
            c.TenantId == state.TenantId &&
            c.RecipientName == "Nguyen Van A" &&
            c.ShippingAddress!.Street == "12 Le Loi" &&
            c.ShippingAddress.City == "Quan 1" &&
            c.ShippingAddress.State == "Ho Chi Minh" &&
            c.Items.Single().ProductId == productId &&
            c.Items.Single().Quantity == 3), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task No_shipment_request_when_the_order_is_missing()
    {
        var order = SalesOrder.Create(Guid.NewGuid(), "x", "x@example.com", new Address(), new List<OrderLineItem>());
        ApprovedSagaAwaitingInventory(order);

        await Saga().HandleInventoryReservedAsync(order.Id, Guid.NewGuid(), default);

        _bus.Verify(b => b.PublishAsync(It.IsAny<CreateShipmentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

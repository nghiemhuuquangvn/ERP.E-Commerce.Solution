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

/// <summary>ROADMAP P1-11: the command that asks Shipping for a shipment carries what a carrier needs - phone, order value, and the cash to collect.</summary>
public class ShipmentCommandContentTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private readonly Mock<ISalesOrderUnitOfWork> _uow = new();
    private readonly Mock<ISalesOrderRepository> _orders = new();
    private readonly Mock<IOrderFulfillmentSagaRepository> _sagas = new();
    private readonly Mock<IMessageBus> _bus = new();
    private readonly List<object> _published = new();

    public ShipmentCommandContentTests()
    {
        _uow.SetupGet(u => u.Orders).Returns(_orders.Object);
        _uow.SetupGet(u => u.Sagas).Returns(_sagas.Object);
        _bus.Setup(b => b.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((m, _) => _published.Add(m)).Returns(Task.CompletedTask);
    }

    private async Task<CreateShipmentCommand> ShipmentRequestFor(string? paymentMethod, string? phone)
    {
        var order = SalesOrder.Create(Guid.NewGuid(), "An Nguyen", "an@example.com", new Address("2 Le Loi", "Quan 3", "Ho Chi Minh", "700000", "Vietnam"),
            new List<OrderLineItem> { new() { ProductId = Guid.NewGuid(), ProductName = "Shirt", Quantity = 2, UnitPrice = 150_000m } },
            orderNo: "SO-9", paymentMethod: paymentMethod, customerPhone: phone);
        _orders.Setup(o => o.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var state = OrderFulfillmentSagaState.Start(order.Id, Tenant);
        state.SkipPaymentForCashOnDelivery();
        state.MarkApproved();
        _sagas.Setup(s => s.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(state);

        await new OrderFulfillmentSaga(_uow.Object, _bus.Object, new Mock<IMediator>().Object, NullLogger<OrderFulfillmentSaga>.Instance)
            .HandleInventoryReservedAsync(order.Id, Guid.NewGuid(), default);

        return _published.OfType<CreateShipmentCommand>().Single();
    }

    [Fact]
    public async Task A_cod_order_asks_for_its_total_as_cash_to_collect_with_the_phone_and_line_prices()
    {
        var command = await ShipmentRequestFor("COD", " 0911 111-111 ");

        command.CodAmount.Should().Be(300_000m);
        command.OrderTotal.Should().Be(300_000m);
        command.PaymentMethod.Should().Be("COD");
        command.RecipientPhone.Should().Be("0911 111-111", "the order keeps what it was given, trimmed; the handler normalizes the phone before it is stored");
        command.Items.Single().UnitPrice.Should().Be(150_000m);
    }

    [Fact]
    public async Task A_prepaid_order_collects_nothing()
    {
        var command = await ShipmentRequestFor("MoMo", null);

        command.CodAmount.Should().Be(0m);
        command.OrderTotal.Should().Be(300_000m);
        command.RecipientPhone.Should().BeNull();
    }

    [Theory]
    [InlineData("0911 111 111", "0911111111")]
    [InlineData("+84 (911) 111-111", "+84911111111")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void A_phone_number_is_stored_as_digits(string? typed, string? stored) => CreateOrderCommandHandler.NormalizePhone(typed).Should().Be(stored);

    [Theory]
    [InlineData("123")]
    [InlineData("call me")]
    [InlineData("0911-111-111-111-111-111")]
    public void Something_that_is_not_a_phone_number_is_refused(string typed)
    {
        var act = () => CreateOrderCommandHandler.NormalizePhone(typed);

        act.Should().Throw<ERP.Shared.Infrastructure.Exceptions.BusinessRuleException>();
    }
}

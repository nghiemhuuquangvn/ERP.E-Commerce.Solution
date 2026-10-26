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

/// <summary>ROADMAP P1-10: the order carries the customer's payment choice; cash on delivery does not wait for a payment.</summary>
public class OnlinePaymentSagaTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private readonly Mock<ISalesOrderUnitOfWork> _uow = new();
    private readonly Mock<ISalesOrderRepository> _orders = new();
    private readonly Mock<IOrderFulfillmentSagaRepository> _sagas = new();
    private readonly Mock<IMessageBus> _bus = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly List<object> _published = new();

    public OnlinePaymentSagaTests()
    {
        _uow.SetupGet(u => u.Orders).Returns(_orders.Object);
        _uow.SetupGet(u => u.Sagas).Returns(_sagas.Object);
        _bus.Setup(b => b.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((m, _) => _published.Add(m)).Returns(Task.CompletedTask);
    }

    private OrderFulfillmentSaga Saga() => new(_uow.Object, _bus.Object, _mediator.Object, NullLogger<OrderFulfillmentSaga>.Instance);

    private SalesOrder Order(string? paymentMethod)
    {
        var order = SalesOrder.Create(Guid.NewGuid(), "A", "a@example.com", new Address("1", "C", "S", "7", "VN"),
            new List<OrderLineItem> { new() { ProductId = Guid.NewGuid(), ProductName = "Shirt", Quantity = 2, UnitPrice = 50000m } },
            orderNo: "SO-1", paymentMethod: paymentMethod);
        _orders.Setup(o => o.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return order;
    }

    // ── what the order records ────────────────────────────────────────────

    [Theory]
    [InlineData("COD", true)]
    [InlineData("cod", true)]
    [InlineData("MoMo", false)]
    [InlineData(null, false)]
    public void Only_a_cod_order_is_cash_on_delivery(string? method, bool cod) => Order(method).IsCashOnDelivery.Should().Be(cod);

    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    [InlineData("momo", "MoMo")]
    [InlineData("zalopay", "ZaloPay")]
    [InlineData("COD", "COD")]
    public void The_payment_method_is_normalized_and_an_unknown_one_is_refused(string? given, string? expected)
        => CreateOrderCommandHandler.NormalizePaymentMethod(given).Should().Be(expected);

    [Fact]
    public void A_payment_method_the_platform_does_not_know_is_a_business_error()
    {
        var act = () => CreateOrderCommandHandler.NormalizePaymentMethod("Bitcoin");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Bitcoin*");
    }

    // ── the payment command ───────────────────────────────────────────────

    [Fact]
    public async Task An_online_order_asks_Payment_for_that_method_in_VND_and_then_waits()
    {
        var order = Order("MoMo");
        SagaState? saved = null;
        _sagas.Setup(s => s.AddAsync(It.IsAny<OrderFulfillmentSagaState>(), It.IsAny<CancellationToken>())).Callback<OrderFulfillmentSagaState, CancellationToken>((s, _) => saved = new SagaState(s)).Returns(Task.CompletedTask);

        await Saga().HandleOrderCreatedAsync(order.Id, Tenant, 100000m, default, order.CustomerId, order.OrderNo, order.PaymentMethod);

        var command = _published.OfType<InitiatePaymentCommand>().Single();
        (command.PaymentMethod, command.Currency, command.CustomerId, command.OrderNumber, command.Amount)
            .Should().Be(("MoMo", "VND", order.CustomerId, "SO-1", 100000m));
        saved!.Step.Should().Be(SagaStep.PaymentPending);
        _published.OfType<ReserveInventoryCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task An_order_that_did_not_say_keeps_the_old_command_and_waits()
    {
        var order = Order(null);

        await Saga().HandleOrderCreatedAsync(order.Id, Tenant, 100000m, default);

        var command = _published.OfType<InitiatePaymentCommand>().Single();
        (command.PaymentMethod, command.Currency).Should().Be((null, null));
        _published.OfType<ReserveInventoryCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task A_cod_order_records_a_pending_payment_and_goes_straight_to_the_stock_reservation()
    {
        var order = Order("COD");
        OrderFulfillmentSagaState? state = null;
        _sagas.Setup(s => s.AddAsync(It.IsAny<OrderFulfillmentSagaState>(), It.IsAny<CancellationToken>())).Callback<OrderFulfillmentSagaState, CancellationToken>((s, _) => state = s).Returns(Task.CompletedTask);

        await Saga().HandleOrderCreatedAsync(order.Id, Tenant, 100000m, default, order.CustomerId, order.OrderNo, "COD");

        _published.OfType<InitiatePaymentCommand>().Single().PaymentMethod.Should().Be("COD");
        _published.OfType<ReserveInventoryCommand>().Single().Items.Single().Quantity.Should().Be(2);
        state!.CurrentStep.Should().Be(SagaStep.InventoryPending);
        state.PaymentId.Should().BeNull();
    }

    [Fact]
    public async Task The_cash_collected_on_delivery_arrives_after_the_saga_moved_on_and_changes_nothing()
    {
        var order = Order("COD");
        var state = OrderFulfillmentSagaState.Start(order.Id, Tenant);
        state.SkipPaymentForCashOnDelivery();
        _sagas.Setup(s => s.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(state);

        await Saga().HandlePaymentProcessedAsync(order.Id, Guid.NewGuid(), default);

        state.CurrentStep.Should().Be(SagaStep.InventoryPending);
        _published.OfType<ReserveInventoryCommand>().Should().BeEmpty();
    }

    // ── compensation ──────────────────────────────────────────────────────

    [Fact]
    public async Task When_a_cod_order_fails_for_lack_of_stock_Payment_is_asked_to_cancel_its_pending_payment_by_order()
    {
        var order = Order("COD");
        var state = OrderFulfillmentSagaState.Start(order.Id, Tenant);
        state.SkipPaymentForCashOnDelivery();
        _sagas.Setup(s => s.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(state);

        await Saga().HandleInventoryInsufficientAsync(order.Id, "no stock", default);

        var refund = _published.OfType<RefundPaymentCommand>().Single();
        (refund.PaymentId, refund.OrderId, refund.TenantId).Should().Be((Guid.Empty, order.Id, Tenant));
    }

    [Fact]
    public async Task An_order_that_did_not_say_and_never_paid_sends_no_refund_as_before()
    {
        var order = Order(null);
        var state = OrderFulfillmentSagaState.Start(order.Id, Tenant);
        state.PaymentInitiated(Guid.NewGuid());
        _sagas.Setup(s => s.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(state);
        var noPayment = OrderFulfillmentSagaState.Start(order.Id, Tenant);
        noPayment.SkipPaymentForCashOnDelivery();
        _sagas.Setup(s => s.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(noPayment);

        await Saga().HandleInventoryInsufficientAsync(order.Id, "no stock", default);

        _published.OfType<RefundPaymentCommand>().Should().BeEmpty();
    }

    /// <summary>The step the saga was in when it was saved (the entity is mutated afterwards).</summary>
    private sealed record SagaState(SagaStep Step)
    {
        public SagaState(OrderFulfillmentSagaState s) : this(s.CurrentStep) { }
    }
}

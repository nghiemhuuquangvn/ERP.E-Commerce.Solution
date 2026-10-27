using ERP.Shared.Messaging;
using ERP.SO.Application.Commands;
using ERP.SO.Application.Handlers;
using ERP.SO.Application.Interfaces;
using ERP.SO.Application.Sagas;
using ERP.SO.Domain.Entities;
using ERP.SO.Domain.Sagas;
using ERP.SO.Domain.ValueObjects;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ERP.SO.UnitTests.Sagas;

/// <summary>ROADMAP P1-5: a saga that stops moving gets its command again, then is compensated; a failed shipment creation compensates; a failed delivery is only recorded.</summary>
public class SagaTimeoutTests
{
    private readonly Mock<ISalesOrderUnitOfWork> _uow = new();
    private readonly Mock<ISalesOrderRepository> _orders = new();
    private readonly Mock<IOrderFulfillmentSagaRepository> _sagas = new();
    private readonly Mock<IMessageBus> _bus = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly SagaOptions _options = new();
    private readonly SalesOrder _order;

    public SagaTimeoutTests()
    {
        _uow.SetupGet(u => u.Orders).Returns(_orders.Object);
        _uow.SetupGet(u => u.Sagas).Returns(_sagas.Object);
        _order = SalesOrder.Create(
            Guid.NewGuid(), "Nguyen Van A", "a@example.com",
            new Address("12 Le Loi", "Quan 1", "Ho Chi Minh", "700000", "Vietnam"),
            new List<OrderLineItem> { new() { ProductId = Guid.NewGuid(), ProductName = "Ao thun", Quantity = 3, UnitPrice = 100m } });
        _orders.Setup(o => o.GetByIdAsync(_order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_order);
    }

    private OrderFulfillmentSaga Saga() => new(_uow.Object, _bus.Object, _mediator.Object, NullLogger<OrderFulfillmentSaga>.Instance);

    private OrderFulfillmentSagaState State(SagaStep step)
    {
        var state = OrderFulfillmentSagaState.Start(_order.Id, Guid.NewGuid());
        if (step >= SagaStep.InventoryPending) state.PaymentInitiated(Guid.NewGuid());
        if (step >= SagaStep.AwaitingApproval) state.RecordInventoryReserved(Guid.NewGuid());
        if (step == SagaStep.ShippingPending) { state.MarkApproved(); state.TryAdvanceToShipping(); }
        _sagas.Setup(s => s.GetByIdAsync(state.Id, It.IsAny<CancellationToken>())).ReturnsAsync(state);
        _sagas.Setup(s => s.GetByOrderIdAsync(_order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(state);
        return state;
    }

    private DateTime Later(OrderFulfillmentSagaState s, int minutes) => s.LastUpdatedAt.AddMinutes(minutes);

    // ---- retry per step ----

    [Fact]
    public async Task A_payment_that_waits_too_long_is_asked_for_again()
    {
        var state = State(SagaStep.PaymentPending);

        var outcome = await Saga().HandleStepTimeoutAsync(state.Id, _options, Later(state, 16), false, default);

        outcome.Should().Be(SagaTimeoutOutcome.Retried);
        state.RetryCount.Should().Be(1);
        _bus.Verify(b => b.PublishAsync(It.Is<InitiatePaymentCommand>(c => c.OrderId == _order.Id && c.TenantId == state.TenantId && c.Amount == _order.TotalAmount), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_stock_reservation_that_waits_too_long_is_requested_again_with_the_lines()
    {
        var state = State(SagaStep.InventoryPending);

        var outcome = await Saga().HandleStepTimeoutAsync(state.Id, _options, Later(state, 11), false, default);

        outcome.Should().Be(SagaTimeoutOutcome.Retried);
        _bus.Verify(b => b.PublishAsync(It.Is<ReserveInventoryCommand>(c => c.OrderId == _order.Id && c.Items.Count == 1 && c.Items[0].Quantity == 3), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_shipment_that_waits_too_long_is_requested_again_with_the_address()
    {
        var state = State(SagaStep.ShippingPending);

        var outcome = await Saga().HandleStepTimeoutAsync(state.Id, _options, Later(state, 16), false, default);

        outcome.Should().Be(SagaTimeoutOutcome.Retried);
        _bus.Verify(b => b.PublishAsync(It.Is<CreateShipmentCommand>(c => c.OrderId == _order.Id && c.ShippingAddress!.City == "Quan 1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(SagaStep.PaymentPending, 14)]
    [InlineData(SagaStep.InventoryPending, 9)]
    [InlineData(SagaStep.ShippingPending, 14)]
    public async Task A_step_inside_its_timeout_is_left_alone(SagaStep step, int minutes)
    {
        var state = State(step);

        var outcome = await Saga().HandleStepTimeoutAsync(state.Id, _options, Later(state, minutes), false, default);

        outcome.Should().Be(SagaTimeoutOutcome.NotDue);
        state.RetryCount.Should().Be(0);
        _bus.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Approval_waits_for_a_person_and_never_times_out_even_when_forced()
    {
        var state = State(SagaStep.AwaitingApproval);

        (await Saga().HandleStepTimeoutAsync(state.Id, _options, Later(state, 100_000), false, default)).Should().Be(SagaTimeoutOutcome.NotDue);
        (await Saga().HandleStepTimeoutAsync(state.Id, _options, Later(state, 100_000), true, default)).Should().Be(SagaTimeoutOutcome.NotDue);
        _bus.VerifyNoOtherCalls();
        _mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_saga_that_is_not_running_or_unknown_is_left_alone()
    {
        var done = State(SagaStep.ShippingPending);
        done.ShipmentCreated(Guid.NewGuid());

        (await Saga().HandleStepTimeoutAsync(done.Id, _options, Later(done, 1000), false, default)).Should().Be(SagaTimeoutOutcome.NotDue);
        (await Saga().HandleStepTimeoutAsync(Guid.NewGuid(), _options, DateTime.UtcNow, false, default)).Should().Be(SagaTimeoutOutcome.NotDue);
    }

    // ---- exhaustion ----

    [Fact]
    public async Task After_the_retries_the_saga_is_compensated_once_with_a_refund_when_paid()
    {
        var state = State(SagaStep.InventoryPending);                // paid, waiting for stock
        for (var i = 0; i < _options.MaxRetries; i++) state.IncrementRetry();

        var outcome = await Saga().HandleStepTimeoutAsync(state.Id, _options, Later(state, 11), false, default);

        outcome.Should().Be(SagaTimeoutOutcome.TimedOut);
        state.Status.Should().Be(SagaStatus.Failed);
        state.ErrorMessage.Should().Contain("Timed out in step InventoryPending after 3 retries");
        _bus.Verify(b => b.PublishAsync(It.Is<RefundPaymentCommand>(c => c.PaymentId == state.PaymentId && c.OrderId == _order.Id), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.Is<CancelOrderCommand>(c => c.OrderId == _order.Id), It.IsAny<CancellationToken>()), Times.Once);

        // a second pass finds a Failed saga and does nothing more
        (await Saga().HandleStepTimeoutAsync(state.Id, _options, Later(state, 100), false, default)).Should().Be(SagaTimeoutOutcome.NotDue);
        _mediator.Verify(m => m.Send(It.IsAny<CancelOrderCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task An_unpaid_order_that_times_out_is_cancelled_and_its_pending_payment_is_closed_not_refunded()
    {
        var state = State(SagaStep.PaymentPending);
        for (var i = 0; i < _options.MaxRetries; i++) state.IncrementRetry();

        await Saga().HandleStepTimeoutAsync(state.Id, _options, Later(state, 16), false, default);

        // No payment id yet (P1-10): Payment is asked by order id, and cancels the payment it still holds as pending instead of refunding money.
        _bus.Verify(b => b.PublishAsync(It.Is<RefundPaymentCommand>(c => c.PaymentId == Guid.Empty && c.OrderId == _order.Id), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<CancelOrderCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_forced_retry_ignores_the_clock_and_the_retry_limit()
    {
        var state = State(SagaStep.ShippingPending);
        for (var i = 0; i < _options.MaxRetries + 2; i++) state.IncrementRetry();

        var outcome = await Saga().HandleStepTimeoutAsync(state.Id, _options, state.LastUpdatedAt, true, default);

        outcome.Should().Be(SagaTimeoutOutcome.Retried);
        state.Status.Should().Be(SagaStatus.Running);
        _bus.Verify(b => b.PublishAsync(It.IsAny<CreateShipmentCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- shipment failures ----

    [Fact]
    public async Task A_shipment_that_cannot_be_created_refunds_cancels_and_fails_the_saga_once()
    {
        var state = State(SagaStep.ShippingPending);

        await Saga().HandleShipmentCreationFailedAsync(_order.Id, "no active carrier", default);
        await Saga().HandleShipmentCreationFailedAsync(_order.Id, "no active carrier", default);      // redelivered

        state.Status.Should().Be(SagaStatus.Failed);
        state.ErrorMessage.Should().Contain("Shipment could not be created: no active carrier");
        _bus.Verify(b => b.PublishAsync(It.IsAny<RefundPaymentCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<CancelOrderCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(SagaStep.PaymentPending)]
    [InlineData(SagaStep.InventoryPending)]
    [InlineData(SagaStep.AwaitingApproval)]
    public async Task A_shipment_failure_before_shipping_was_requested_changes_nothing(SagaStep step)
    {
        var state = State(step);

        await Saga().HandleShipmentCreationFailedAsync(_order.Id, "x", default);

        state.Status.Should().Be(SagaStatus.Running);
        _mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_failed_delivery_attempt_is_recorded_and_the_order_is_not_cancelled()
    {
        var state = State(SagaStep.ShippingPending);
        state.ShipmentCreated(Guid.NewGuid());

        await Saga().HandleDeliveryFailedAsync(_order.Id, "customer not at home", 2, default);

        state.Status.Should().Be(SagaStatus.Completed);
        state.ErrorMessage.Should().Be("Delivery failed (attempt 2): customer not at home");
        _mediator.VerifyNoOtherCalls();
        _bus.VerifyNoOtherCalls();
    }

    // ---- options and the page's view ----

    [Fact]
    public void The_default_timeouts_and_the_shortest_one()
    {
        _options.TimeoutFor(SagaStep.PaymentPending).Should().Be(TimeSpan.FromMinutes(15));
        _options.TimeoutFor(SagaStep.InventoryPending).Should().Be(TimeSpan.FromMinutes(10));
        _options.TimeoutFor(SagaStep.ShippingPending).Should().Be(TimeSpan.FromMinutes(15));
        _options.TimeoutFor(SagaStep.AwaitingApproval).Should().BeNull();
        _options.TimeoutFor(SagaStep.Completed).Should().BeNull();
        _options.ShortestTimeout.Should().Be(TimeSpan.FromMinutes(10));
        _options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void The_page_sees_the_steps_the_retry_button_and_a_stuck_step()
    {
        var state = State(SagaStep.InventoryPending);

        var dto = OrderSagaMapper.Map(_order.Id, state, _options, state.LastUpdatedAt.AddMinutes(12));

        dto.Steps.Select(s => (s.Key, s.State)).Should().Equal(("Payment", "Done"), ("Inventory", "Current"), ("Approval", "Waiting"), ("Shipping", "Waiting"), ("Done", "Waiting"));
        (dto.Stuck, dto.CanRetry, dto.TimeoutMinutes, dto.MaxRetries).Should().Be((true, true, 10, 3));
    }

    [Fact]
    public void An_approval_done_early_shows_as_done_and_a_waiting_approval_has_no_retry()
    {
        var early = State(SagaStep.InventoryPending);
        early.MarkApproved();
        OrderSagaMapper.Map(_order.Id, early, _options, DateTime.UtcNow).Steps.Single(s => s.Key == "Approval").State.Should().Be("Done");

        var waiting = State(SagaStep.AwaitingApproval);
        var dto = OrderSagaMapper.Map(_order.Id, waiting, _options, DateTime.UtcNow.AddDays(30));
        dto.Steps.Select(s => s.State).Should().Equal("Done", "Done", "Current", "Waiting", "Waiting");
        (dto.CanRetry, dto.Stuck).Should().Be((false, false));
    }

    [Fact]
    public void A_failed_saga_marks_the_step_it_stopped_at_and_a_completed_one_is_all_done()
    {
        var failed = State(SagaStep.InventoryPending);
        failed.BeginCompensation("Inventory insufficient: x");
        failed.MarkFailed("Inventory insufficient: x");
        var dto = OrderSagaMapper.Map(_order.Id, failed, _options, DateTime.UtcNow);
        dto.Steps.Select(s => s.State).Should().Equal("Done", "Failed", "Waiting", "Waiting", "Waiting");
        (dto.Status, dto.CanRetry, dto.Message).Should().Be(("Failed", false, "Inventory insufficient: x"));

        var done = State(SagaStep.ShippingPending);
        done.ShipmentCreated(Guid.NewGuid());
        OrderSagaMapper.Map(_order.Id, done, _options, DateTime.UtcNow).Steps.Select(s => s.State).Should().OnlyContain(s => s == "Done");
    }

    [Fact]
    public void An_order_without_a_saga_has_nothing_to_show()
    {
        OrderSagaMapper.Map(Guid.NewGuid(), null, _options, DateTime.UtcNow).Exists.Should().BeFalse();
    }

    [Fact]
    public async Task The_retry_command_refuses_a_finished_saga_and_the_approval_step()
    {
        var done = State(SagaStep.ShippingPending);
        done.ShipmentCreated(Guid.NewGuid());
        var handler = new RetryOrderSagaCommandHandler(_uow.Object, Saga(), Options.Create(_options));

        var finished = () => handler.Handle(new RetryOrderSagaCommand { OrderId = _order.Id }, default);
        await finished.Should().ThrowAsync<ERP.Shared.Infrastructure.Exceptions.BusinessRuleException>().WithMessage("*nothing to retry*");

        State(SagaStep.AwaitingApproval);
        var approval = () => handler.Handle(new RetryOrderSagaCommand { OrderId = _order.Id }, default);
        await approval.Should().ThrowAsync<ERP.Shared.Infrastructure.Exceptions.BusinessRuleException>().WithMessage("*done by a person*");
    }
}

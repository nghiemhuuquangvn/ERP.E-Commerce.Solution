using ERP.PAY.Application.Commands;
using ERP.PAY.Application.Handlers;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Entities;
using ERP.PAY.Domain.Enums;
using ERP.PAY.Domain.Events;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace ERP.PAY.UnitTests.OnlinePayments;

/// <summary>ROADMAP R43: the cash of an order in several parcels is captured parcel by parcel.</summary>
public class PartialCodCaptureTests
{
    private readonly Mock<IPaymentUnitOfWork> _uow = new();
    private readonly Mock<IPaymentRepository> _payments = new();

    public PartialCodCaptureTests() => _uow.SetupGet(u => u.Payments).Returns(_payments.Object);

    private Payment CodPayment(decimal amount = 200_000m)
    {
        var payment = Payment.Initiate(Guid.NewGuid(), amount, PaymentMethod.Cash, "VND", null, PaymentProviders.Cod, Guid.NewGuid(), "SO-1");
        _payments.Setup(r => r.GetByOrderIdAsync(payment.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        return payment;
    }

    private Task<bool> Deliver(Payment p, Guid shipment, decimal cod, int pending) =>
        new CollectCashOnDeliveryCommandHandler(_uow.Object).Handle(
            new CollectCashOnDeliveryCommand { OrderId = p.OrderId, ShipmentId = shipment, Amount = cod, OtherParcelsPending = pending }, default);

    // ── the aggregate ─────────────────────────────────────────────────────

    [Fact]
    public void Two_parcels_make_the_payment_partially_captured_then_captured_with_one_event_each()
    {
        var p = CodPayment();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        p.CapturePart(first, 100_000m).Should().NotBeNull();
        p.Status.Should().Be(PaymentStatus.PartiallyCaptured);
        p.CapturedAmount.Should().Be(100_000m);
        p.GatewayProvider.Should().Be("COD");

        p.CapturePart(second, 100_000m).Should().NotBeNull();
        p.Status.Should().Be(PaymentStatus.Captured);
        p.CapturedAmount.Should().Be(200_000m);

        var events = p.DomainEvents.OfType<PaymentProcessedEvent>().ToList();
        events.Should().HaveCount(2);
        events.Select(e => e.CaptureId).Should().Equal(first, second);
        events.Select(e => e.Amount).Should().Equal(100_000m, 100_000m);
        events.Select(e => e.CapturedTotal).Should().Equal(100_000m, 200_000m);
    }

    [Fact]
    public void A_parcel_is_captured_once()
    {
        var p = CodPayment();
        var shipment = Guid.NewGuid();
        p.CapturePart(shipment, 50_000m);

        p.CapturePart(shipment, 50_000m).Should().BeNull();

        p.CapturedAmount.Should().Be(50_000m);
        p.DomainEvents.OfType<PaymentProcessedEvent>().Should().ContainSingle();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(200_001)]
    public void A_part_must_be_positive_and_never_above_what_is_left(decimal amount)
    {
        var p = CodPayment();
        var act = () => p.CapturePart(Guid.NewGuid(), amount);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void The_single_capture_of_a_gateway_payment_is_unchanged_and_counts_as_collected()
    {
        var p = Payment.Initiate(Guid.NewGuid(), 150_000m, PaymentMethod.EWallet, "VND", null, "MoMo");
        p.Authorize("REF", "MoMo");
        p.Capture();

        p.CapturedAmount.Should().Be(150_000m);
        var evt = p.DomainEvents.OfType<PaymentProcessedEvent>().Single();
        evt.Amount.Should().Be(150_000m);
        evt.CaptureId.Should().BeNull();
        evt.CapturedTotal.Should().Be(150_000m);
    }

    [Fact]
    public void A_partly_collected_payment_cannot_be_cancelled_or_failed()
    {
        var p = CodPayment();
        p.CapturePart(Guid.NewGuid(), 100_000m);

        ((Action)p.Cancel).Should().Throw<InvalidOperationException>();
        ((Action)(() => p.Fail("x"))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_refund_is_capped_at_what_was_collected()
    {
        var p = CodPayment();
        p.CapturePart(Guid.NewGuid(), 100_000m);

        var tooMuch = () => p.AddRefund(100_001m, "r");
        tooMuch.Should().Throw<InvalidOperationException>();

        var refund = p.AddRefund(100_000m, "return");
        p.CompleteRefund(refund, "G1");
        p.Status.Should().Be(PaymentStatus.Refunded);      // everything collected is back
    }

    [Fact]
    public void A_failed_refund_of_a_partly_collected_payment_returns_to_partially_captured()
    {
        var p = CodPayment();
        p.CapturePart(Guid.NewGuid(), 100_000m);
        var refund = p.AddRefund(40_000m, "return");
        refund.MarkAsFailed("declined");

        p.ReconcileStatusWithConfirmedRefunds();

        p.Status.Should().Be(PaymentStatus.PartiallyCaptured);
    }

    // ── the command ───────────────────────────────────────────────────────

    [Fact]
    public async Task Each_delivered_parcel_collects_its_own_cash_and_a_replay_changes_nothing()
    {
        var p = CodPayment();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        (await Deliver(p, a, 120_000m, pending: 1)).Should().BeTrue();
        p.Status.Should().Be(PaymentStatus.PartiallyCaptured);

        (await Deliver(p, a, 120_000m, pending: 1)).Should().BeFalse();   // the same message again
        p.CapturedAmount.Should().Be(120_000m);

        (await Deliver(p, b, 80_000m, pending: 0)).Should().BeTrue();
        p.Status.Should().Be(PaymentStatus.Captured);
        (await Deliver(p, Guid.NewGuid(), 80_000m, pending: 0)).Should().BeFalse();   // nothing left to collect
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task A_parcel_cannot_collect_more_than_what_is_left()
    {
        var p = CodPayment(150_000m);
        await Deliver(p, Guid.NewGuid(), 100_000m, pending: 1);

        await Deliver(p, Guid.NewGuid(), 100_000m, pending: 0);

        p.CapturedAmount.Should().Be(150_000m);
        p.Status.Should().Be(PaymentStatus.Captured);
    }

    [Fact]
    public async Task A_message_without_the_parcels_cash_collects_what_is_left_only_with_the_last_parcel()
    {
        var p = CodPayment();

        (await Deliver(p, Guid.NewGuid(), cod: 0m, pending: 1)).Should().BeFalse();   // not the last, cash unknown: nothing yet
        p.Status.Should().Be(PaymentStatus.Pending);

        (await Deliver(p, Guid.NewGuid(), cod: 0m, pending: 0)).Should().BeTrue();    // behaves as before R43
        p.Status.Should().Be(PaymentStatus.Captured);
        p.CapturedAmount.Should().Be(200_000m);
    }

    [Fact]
    public async Task An_unsplit_order_still_collects_its_whole_cash_once()
    {
        var p = CodPayment();
        var shipment = Guid.NewGuid();

        (await Deliver(p, shipment, 200_000m, pending: 0)).Should().BeTrue();
        (await Deliver(p, shipment, 200_000m, pending: 0)).Should().BeFalse();

        p.Status.Should().Be(PaymentStatus.Captured);
        p.DomainEvents.OfType<PaymentProcessedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task An_online_payment_or_an_unknown_order_is_left_alone()
    {
        var online = Payment.Initiate(Guid.NewGuid(), 90_000m, PaymentMethod.EWallet, "VND", null, "MoMo");
        _payments.Setup(r => r.GetByOrderIdAsync(online.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(online);

        (await Deliver(online, Guid.NewGuid(), 90_000m, 0)).Should().BeFalse();
        online.Status.Should().Be(PaymentStatus.Pending);
        (await new CollectCashOnDeliveryCommandHandler(_uow.Object).Handle(new CollectCashOnDeliveryCommand { OrderId = Guid.NewGuid(), Amount = 1 }, default)).Should().BeFalse();
    }

    // ── returns ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_return_refunds_at_most_what_was_collected()
    {
        var p = CodPayment();
        await Deliver(p, Guid.NewGuid(), 100_000m, pending: 1);
        var mediator = new Mock<IMediator>();
        var handler = new RefundReturnedOrderCommandHandler(_uow.Object, mediator.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<RefundReturnedOrderCommandHandler>.Instance);

        var result = await handler.Handle(new RefundReturnedOrderCommand { OrderId = p.OrderId, RefundAmount = 200_000m }, default);

        result.Should().Be(RefundReturnedOrderResult.Requested);
        mediator.Verify(m => m.Send(It.Is<RefundPaymentCommand>(c => c.PaymentId == p.Id && c.Amount == 100_000m), It.IsAny<CancellationToken>()), Times.Once);
    }
}

using ERP.PAY.Application.Commands;
using ERP.PAY.Application.Handlers;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Entities;
using ERP.PAY.Domain.Enums;
using ERP.PAY.Domain.Events;
using ERP.PAY.Infrastructure.MessageBus;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.PAY.UnitTests.Refunds;

/// <summary>ROADMAP P1-1e: PaymentRefundedEvent exists only for a refund the gateway confirmed.</summary>
public class RefundEventTests
{
    private static Payment CapturedPayment(decimal amount = 1000m, PaymentMethod method = PaymentMethod.BankTransfer)
    {
        var payment = Payment.Initiate(Guid.NewGuid(), amount, method, "VND");
        payment.Authorize("GW-1", "VNPay");
        payment.Capture();
        payment.ClearDomainEvents();
        return payment;
    }

    [Fact]
    public void Adding_a_refund_publishes_nothing_until_the_gateway_confirms()
    {
        var payment = CapturedPayment();

        payment.AddRefund(400m, "customer request");

        payment.DomainEvents.OfType<PaymentRefundedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void A_confirmed_refund_publishes_the_event_with_the_refund_id_method_and_confirmation_time()
    {
        var payment = CapturedPayment(1000m, PaymentMethod.Cash);
        var refund = payment.AddRefund(400m, "customer request");

        payment.CompleteRefund(refund, "GWR-9");

        var evt = payment.DomainEvents.OfType<PaymentRefundedEvent>().Single();
        evt.RefundId.Should().Be(refund.Id);
        evt.RefundAmount.Should().Be(400m);
        evt.Method.Should().Be(PaymentMethod.Cash);
        evt.Currency.Should().Be("VND");
        evt.GatewayRefundId.Should().Be("GWR-9");
        evt.ProcessedAt.Should().NotBeNull();
        payment.Status.Should().Be(PaymentStatus.PartiallyRefunded);
    }

    [Fact]
    public void Two_partial_refunds_give_two_events_with_different_ids_and_the_second_completes_the_refund()
    {
        var payment = CapturedPayment();
        var first = payment.AddRefund(400m, "a");
        payment.CompleteRefund(first, "GWR-1");
        var second = payment.AddRefund(600m, "b");
        payment.CompleteRefund(second, "GWR-2");

        var events = payment.DomainEvents.OfType<PaymentRefundedEvent>().ToList();
        events.Should().HaveCount(2);
        events.Select(e => e.RefundId).Distinct().Should().HaveCount(2);
        payment.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public void A_refund_above_the_captured_amount_is_refused()
    {
        var payment = CapturedPayment();
        var refund = payment.AddRefund(900m, "a");
        payment.CompleteRefund(refund, "GWR-1");

        var act = () => payment.AddRefund(200m, "b");

        act.Should().Throw<InvalidOperationException>();
    }

    private static (RefundPaymentCommandHandler Handler, Payment Payment, Mock<IPaymentUnitOfWork> Uow) HandlerFor(GatewayRefundResult result)
    {
        var payment = CapturedPayment();
        var repo = new Mock<IPaymentRepository>();
        repo.Setup(r => r.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        var uow = new Mock<IPaymentUnitOfWork>();
        uow.SetupGet(u => u.Payments).Returns(repo.Object);
        var gateway = new Mock<IPaymentProvider>();
        gateway.Setup(g => g.RefundAsync(It.IsAny<GatewayCredentials>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        var providers = new Mock<IPaymentProviderResolver>();
        providers.Setup(r => r.StubFor(It.IsAny<string?>())).Returns((IPaymentProvider?)null);
        providers.Setup(r => r.ResolveAsync("VNPay", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolvedProvider(gateway.Object, new GatewayCredentials("VNPay", "TMN", null, "secret", null, true)));
        return (new RefundPaymentCommandHandler(uow.Object, providers.Object), payment, uow);
    }

    [Fact]
    public async Task A_gateway_failure_leaves_the_payment_captured_and_publishes_no_refund_event()
    {
        var (handler, payment, uow) = HandlerFor(new GatewayRefundResult(false, null, "declined"));

        await handler.Handle(new RefundPaymentCommand { PaymentId = payment.Id, Amount = 400m, Reason = "x" }, default);

        payment.DomainEvents.OfType<PaymentRefundedEvent>().Should().BeEmpty();
        payment.Status.Should().Be(PaymentStatus.Captured);
        payment.Refunds.Single().Status.Should().Be(RefundStatus.Failed);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_gateway_success_publishes_exactly_one_refund_event()
    {
        var (handler, payment, _) = HandlerFor(new GatewayRefundResult(true, "GWR-7", null));

        await handler.Handle(new RefundPaymentCommand { PaymentId = payment.Id, Amount = 400m, Reason = "x" }, default);

        payment.DomainEvents.OfType<PaymentRefundedEvent>().Should().ContainSingle()
            .Which.GatewayRefundId.Should().Be("GWR-7");
    }

    [Fact]
    public void The_events_of_one_payment_share_a_kafka_key()
    {
        var payment = CapturedPayment();
        var refund = payment.AddRefund(100m, "x");
        payment.CompleteRefund(refund, "g");
        var refunded = payment.DomainEvents.OfType<PaymentRefundedEvent>().Single();

        KafkaMessageBus.PartitionKey(refunded).Should().Be(payment.Id.ToString());
    }
}

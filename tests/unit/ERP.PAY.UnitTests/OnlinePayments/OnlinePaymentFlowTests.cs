using ERP.PAY.Application.Commands;
using ERP.PAY.Application.DTOs;
using ERP.PAY.Application.Handlers;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Entities;
using ERP.PAY.Domain.Enums;
using ERP.PAY.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;
using static ERP.PAY.UnitTests.Gateways.GatewayTestSupport;

namespace ERP.PAY.UnitTests.OnlinePayments;

/// <summary>ROADMAP P1-10: callback amount check, checkout, status, cash on delivery, and the notification pipeline.</summary>
public class OnlinePaymentFlowTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Customer = Guid.NewGuid();

    private readonly Mock<IPaymentUnitOfWork> _uow = new();
    private readonly Mock<IPaymentRepository> _payments = new();
    private readonly Mock<IPaymentProviderResolver> _providers = new();
    private readonly Mock<IPaymentProvider> _momo = new();

    public OnlinePaymentFlowTests()
    {
        _uow.SetupGet(u => u.Payments).Returns(_payments.Object);
        _momo.SetupGet(p => p.Code).Returns("MoMo");
        _momo.Setup(p => p.NewTransactionRef(It.IsAny<Guid>(), It.IsAny<DateTime>())).Returns(() => "MREF-" + Guid.NewGuid().ToString("N")[..6]);
        _providers.Setup(r => r.ResolveAsync("MoMo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolvedProvider(_momo.Object, new GatewayCredentials("MoMo", "P", "A", "S", null, true)));
    }

    private Payment Pending(string provider = "MoMo", decimal amount = 150000m, Guid? customer = null)
    {
        var payment = Payment.Initiate(Guid.NewGuid(), amount, PaymentProviders.MethodFor(provider), "VND", null, provider, customer ?? Customer, "SO-77");
        _payments.Setup(r => r.GetByOrderIdAsync(payment.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        _payments.Setup(r => r.GetByTransactionRefAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        return payment;
    }

    // ── initiate ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("COD", PaymentMethod.Cash)]
    [InlineData("momo", PaymentMethod.EWallet)]
    [InlineData("ZaloPay", PaymentMethod.EWallet)]
    [InlineData("VNPay", PaymentMethod.EWallet)]
    [InlineData(null, PaymentMethod.CreditCard)]          // an order that did not say keeps the old behaviour
    [InlineData("Unspecified", PaymentMethod.CreditCard)]
    public async Task The_payment_takes_its_method_from_what_the_customer_chose(string? provider, PaymentMethod expected)
    {
        Payment? created = null;
        _payments.Setup(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>())).Callback<Payment, CancellationToken>((p, _) => created = p).Returns(Task.CompletedTask);
        var fraud = new Mock<IFraudDetectionService>();
        fraud.Setup(f => f.EvaluateAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>())).ReturnsAsync(new FraudCheckResult(false, null));

        await new InitiatePaymentCommandHandler(_uow.Object, fraud.Object).Handle(new InitiatePaymentCommand
        {
            OrderId = Guid.NewGuid(), Amount = 100000m, Currency = "VND", Method = PaymentMethod.CreditCard,
            Provider = provider, CustomerId = Customer, OrderNumber = "SO-1"
        }, default);

        created!.Method.Should().Be(expected);
        created.CustomerId.Should().Be(Customer);
        created.OrderNumber.Should().Be("SO-1");
    }

    // ── callback amount ───────────────────────────────────────────────────

    [Fact]
    public async Task A_paid_callback_for_the_right_amount_captures_the_payment()
    {
        var payment = Pending();

        await new ProcessGatewayCallbackCommandHandler(_uow.Object).Handle(new ProcessGatewayCallbackCommand
        { TransactionRef = "R", IsPaid = true, GatewayTransactionId = "T1", GatewayProvider = "MoMo", Amount = 150000m }, default);

        payment.Status.Should().Be(PaymentStatus.Captured);
        payment.GatewayProvider.Should().Be("MoMo");
    }

    [Fact]
    public async Task A_paid_callback_for_another_amount_captures_nothing_flags_the_payment_and_is_refused()
    {
        var payment = Pending();

        var act = () => new ProcessGatewayCallbackCommandHandler(_uow.Object).Handle(new ProcessGatewayCallbackCommand
        { TransactionRef = "R", IsPaid = true, GatewayTransactionId = "T1", GatewayProvider = "MoMo", Amount = 1000m }, default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*amount*");
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.IsFlaggedForReview.Should().BeTrue();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);   // the flag is saved
    }

    [Fact]
    public async Task A_repeated_paid_callback_changes_nothing()
    {
        var payment = Pending();
        var handler = new ProcessGatewayCallbackCommandHandler(_uow.Object);
        var command = new ProcessGatewayCallbackCommand { TransactionRef = "R", IsPaid = true, GatewayTransactionId = "T1", GatewayProvider = "MoMo", Amount = 150000m };

        await handler.Handle(command, default);
        payment.ClearDomainEvents();
        await handler.Handle(command, default);

        payment.DomainEvents.Should().BeEmpty();
        payment.Status.Should().Be(PaymentStatus.Captured);
    }

    [Fact]
    public async Task A_failed_callback_fails_the_payment()
    {
        var payment = Pending();

        await new ProcessGatewayCallbackCommandHandler(_uow.Object).Handle(new ProcessGatewayCallbackCommand
        { TransactionRef = "R", IsPaid = false, GatewayProvider = "MoMo", FailureReason = "user cancelled" }, default);

        payment.Status.Should().Be(PaymentStatus.Failed);
    }

    // ── checkout ──────────────────────────────────────────────────────────

    private StartCheckoutCommandHandler Checkout()
    {
        StartCheckoutCommandHandler.WaitAttempts = 1;
        StartCheckoutCommandHandler.WaitStep = TimeSpan.Zero;
        return new StartCheckoutCommandHandler(_uow.Object, _providers.Object);
    }

    private void MomoCreates(string url = "https://pay.momo.test/x") =>
        _momo.Setup(p => p.CreatePaymentAsync(It.IsAny<GatewayCredentials>(), It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GatewayCredentials _, GatewayPaymentRequest r, CancellationToken __) => new GatewayCreateResult(true, url, r.TransactionRef, null));

    [Fact]
    public async Task The_customer_gets_the_gateway_page_and_the_payment_keeps_the_new_reference()
    {
        var payment = Pending();
        MomoCreates();
        GatewayPaymentRequest? sent = null;
        _momo.Setup(p => p.CreatePaymentAsync(It.IsAny<GatewayCredentials>(), It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GatewayCredentials, GatewayPaymentRequest, CancellationToken>((_, r, _) => sent = r)
            .ReturnsAsync((GatewayCredentials _, GatewayPaymentRequest r, CancellationToken __) => new GatewayCreateResult(true, "https://pay.momo.test/x", r.TransactionRef, null));

        var result = await Checkout().Handle(new StartCheckoutCommand { OrderId = payment.OrderId, CallerId = Customer, PublicBaseUrl = "https://shop.test/" }, default);

        result.PaymentUrl.Should().Be("https://pay.momo.test/x");
        result.Provider.Should().Be("MoMo");
        payment.TransactionRef.Should().Be(result.TransactionRef);
        sent!.ReturnUrl.Should().Be("https://shop.test/api/v1/payment-gateway/callback/momo");
        sent.NotifyUrl.Should().Be("https://shop.test/api/v1/payment-gateway/ipn/momo");
        sent.Amount.Should().Be(150000m);
    }

    [Fact]
    public async Task Starting_again_while_pending_gives_a_fresh_reference()
    {
        var payment = Pending();
        MomoCreates();
        var handler = Checkout();
        var command = new StartCheckoutCommand { OrderId = payment.OrderId, CallerId = Customer, PublicBaseUrl = "https://shop.test" };

        var first = await handler.Handle(command, default);
        var second = await handler.Handle(command, default);

        second.TransactionRef.Should().NotBe(first.TransactionRef);
        payment.TransactionRef.Should().Be(second.TransactionRef);
    }

    [Fact]
    public async Task Someone_elses_order_looks_like_a_missing_one_but_staff_may_start_it()
    {
        var payment = Pending(customer: Guid.NewGuid());
        MomoCreates();
        var handler = Checkout();

        var other = () => handler.Handle(new StartCheckoutCommand { OrderId = payment.OrderId, CallerId = Customer, PublicBaseUrl = "https://s" }, default);
        await other.Should().ThrowAsync<KeyNotFoundException>();

        var staff = await handler.Handle(new StartCheckoutCommand { OrderId = payment.OrderId, CallerId = Customer, CallerIsStaff = true, PublicBaseUrl = "https://s" }, default);
        staff.PaymentUrl.Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_cod_order_or_an_already_captured_payment_cannot_be_started()
    {
        var cod = Pending("COD");
        var handler = Checkout();
        (await FluentActions.Awaiting(() => handler.Handle(new StartCheckoutCommand { OrderId = cod.OrderId, CallerId = Customer, PublicBaseUrl = "https://s" }, default))
            .Should().ThrowAsync<InvalidOperationException>()).WithMessage("*on delivery*");

        var paid = Pending();
        paid.Authorize("T", "MoMo");
        paid.Capture();
        (await FluentActions.Awaiting(() => handler.Handle(new StartCheckoutCommand { OrderId = paid.OrderId, CallerId = Customer, PublicBaseUrl = "https://s" }, default))
            .Should().ThrowAsync<InvalidOperationException>()).WithMessage("*Captured*");
    }

    [Fact]
    public async Task A_gateway_that_refuses_leaves_the_previous_reference_alone()
    {
        var payment = Pending();
        payment.AssignTransactionRef("OLD");
        _momo.Setup(p => p.CreatePaymentAsync(It.IsAny<GatewayCredentials>(), It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayCreateResult(false, "", "", "MoMo error 99"));

        var act = () => Checkout().Handle(new StartCheckoutCommand { OrderId = payment.OrderId, CallerId = Customer, PublicBaseUrl = "https://s" }, default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*99*");
        payment.TransactionRef.Should().Be("OLD");
    }

    [Fact]
    public async Task A_payment_the_saga_has_not_created_yet_is_reported_as_not_ready()
    {
        var act = () => Checkout().Handle(new StartCheckoutCommand { OrderId = Guid.NewGuid(), CallerId = Customer, PublicBaseUrl = "https://s" }, default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not ready*");
    }

    // ── status ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Status_is_visible_to_the_orders_customer_and_to_staff_only()
    {
        var payment = Pending();
        var handler = new GetOrderPaymentStatusQueryHandler(_uow.Object);

        (await handler.Handle(new GetOrderPaymentStatusQuery { OrderId = payment.OrderId, CallerId = Customer }, default))!.Status.Should().Be("Pending");
        (await handler.Handle(new GetOrderPaymentStatusQuery { OrderId = payment.OrderId, CallerId = Guid.NewGuid() }, default)).Should().BeNull();
        (await handler.Handle(new GetOrderPaymentStatusQuery { OrderId = payment.OrderId, CallerId = Guid.NewGuid(), CallerIsStaff = true }, default)).Should().NotBeNull();
    }

    // ── cash on delivery ──────────────────────────────────────────────────

    [Fact]
    public async Task Delivery_captures_a_pending_cod_payment_once()
    {
        var payment = Pending("COD");
        var handler = new CollectCashOnDeliveryCommandHandler(_uow.Object);

        (await handler.Handle(new CollectCashOnDeliveryCommand { OrderId = payment.OrderId }, default)).Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Captured);
        payment.GatewayProvider.Should().Be("COD");
        payment.DomainEvents.OfType<ERP.PAY.Domain.Events.PaymentProcessedEvent>().Should().ContainSingle();

        (await handler.Handle(new CollectCashOnDeliveryCommand { OrderId = payment.OrderId }, default)).Should().BeFalse();   // redelivered event
    }

    [Fact]
    public async Task Delivery_does_not_touch_an_online_payment_or_an_unknown_order()
    {
        var online = Pending("MoMo");
        var handler = new CollectCashOnDeliveryCommandHandler(_uow.Object);

        (await handler.Handle(new CollectCashOnDeliveryCommand { OrderId = online.OrderId }, default)).Should().BeFalse();
        online.Status.Should().Be(PaymentStatus.Pending);
        (await handler.Handle(new CollectCashOnDeliveryCommand { OrderId = Guid.NewGuid() }, default)).Should().BeFalse();
    }

    // ── the notification pipeline ─────────────────────────────────────────

    private (GatewayNotificationService Service, Mock<IMediator> Mediator) Pipeline(GatewayVerifyResult verify, string? peeked = "MREF-1", Guid? tenant = null)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ProcessGatewayCallbackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentDto { OrderId = Guid.NewGuid() });
        _providers.Setup(r => r.Peek("MoMo", It.IsAny<GatewayCallback>())).Returns(peeked);
        _providers.Setup(r => r.ReplyFor("MoMo", It.IsAny<bool>(), It.IsAny<string>())).Returns(new GatewayIpnReply(400, new { message = "no" }));
        _momo.Setup(p => p.Verify(It.IsAny<GatewayCredentials>(), It.IsAny<GatewayCallback>())).Returns(verify);
        _momo.Setup(p => p.IpnResponse(It.IsAny<bool>(), It.IsAny<string>())).Returns(new GatewayIpnReply(204, null));
        _payments.Setup(r => r.FindTenantByTransactionRefAsync("MREF-1", It.IsAny<CancellationToken>())).ReturnsAsync(tenant ?? Tenant);
        return (new GatewayNotificationService(_payments.Object, _providers.Object, mediator.Object, Log<GatewayNotificationService>()), mediator);
    }

    private static readonly GatewayCallback Anything = new("POST", new Dictionary<string, string>(), "{}");

    [Fact]
    public async Task A_verified_paid_notification_is_applied_with_its_amount_and_answered_as_accepted()
    {
        var (service, mediator) = Pipeline(new GatewayVerifyResult(true, true, "MREF-1", "T9", null, 150000m));

        var result = await service.ProcessAsync("momo", Anything);

        result.Outcome.Should().Be(NotificationOutcome.Paid);
        result.Reply.StatusCode.Should().Be(204);
        mediator.Verify(m => m.Send(It.Is<ProcessGatewayCallbackCommand>(c => c.TransactionRef == "MREF-1" && c.IsPaid && c.Amount == 150000m && c.GatewayProvider == "MoMo"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_bad_signature_or_a_reference_that_differs_from_the_peeked_one_applies_nothing()
    {
        var (bad, mediatorA) = Pipeline(new GatewayVerifyResult(false, false, "", null, "Signature mismatch"));
        (await bad.ProcessAsync("MoMo", Anything)).Outcome.Should().Be(NotificationOutcome.InvalidSignature);

        var (swapped, mediatorB) = Pipeline(new GatewayVerifyResult(true, true, "SOMEONE-ELSES-REF", "T", null, 1m));
        (await swapped.ProcessAsync("MoMo", Anything)).Outcome.Should().Be(NotificationOutcome.InvalidSignature);

        mediatorA.Verify(m => m.Send(It.IsAny<ProcessGatewayCallbackCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        mediatorB.Verify(m => m.Send(It.IsAny<ProcessGatewayCallbackCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_unknown_reference_or_a_message_without_one_is_rejected_before_any_key_is_used()
    {
        var (unknown, _) = Pipeline(new GatewayVerifyResult(true, true, "MREF-1", "T", null, 1m), tenant: null);
        _payments.Setup(r => r.FindTenantByTransactionRefAsync("MREF-1", It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        (await unknown.ProcessAsync("MoMo", Anything)).Outcome.Should().Be(NotificationOutcome.UnknownReference);

        var (none, _) = Pipeline(new GatewayVerifyResult(true, true, "MREF-1", "T", null, 1m), peeked: null);
        (await none.ProcessAsync("MoMo", Anything)).Outcome.Should().Be(NotificationOutcome.UnknownReference);
        _providers.Verify(r => r.ResolveAsync("MoMo", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_amount_mismatch_is_reported_not_applied_and_answered_as_refused()
    {
        var (service, mediator) = Pipeline(new GatewayVerifyResult(true, true, "MREF-1", "T", null, 1m));
        mediator.Setup(m => m.Send(It.IsAny<ProcessGatewayCallbackCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The paid amount does not match the payment."));
        _providers.Setup(r => r.ReplyFor(It.IsAny<string>(), false, It.IsAny<string>())).Returns(new GatewayIpnReply(400, new { message = "no" }));
        _momo.Setup(p => p.IpnResponse(false, It.IsAny<string>())).Returns(new GatewayIpnReply(400, new { message = "Amount mismatch" }));

        var result = await service.ProcessAsync("MoMo", Anything);

        result.Outcome.Should().Be(NotificationOutcome.AmountMismatch);
        result.Accepted.Should().BeFalse();
        result.Reply.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task A_provider_that_is_not_an_online_gateway_is_refused()
    {
        var (service, _) = Pipeline(new GatewayVerifyResult(true, true, "MREF-1", "T", null, 1m));

        await FluentActions.Awaiting(() => service.ProcessAsync("COD", Anything)).Should().ThrowAsync<ERP.Shared.Infrastructure.Exceptions.BusinessRuleException>();
    }
}

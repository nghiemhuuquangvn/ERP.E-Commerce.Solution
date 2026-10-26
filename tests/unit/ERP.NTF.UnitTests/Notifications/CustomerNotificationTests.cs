using ERP.NTF.Application.Commands;
using ERP.NTF.Application.EventHandlers;
using ERP.NTF.Application.Handlers;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Entities;
using ERP.NTF.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MediatR;
using Moq;
using Xunit;

namespace ERP.NTF.UnitTests.Notifications;

/// <summary>ROADMAP P1-6: customer messages reach the customer of the order, are rendered, and are sent once.</summary>
public class CustomerNotificationTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IRecipientRepository> _recipients = new();
    private readonly List<SendNotificationCommand> _sent = new();
    private readonly Guid _orderId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    public CustomerNotificationTests()
    {
        _mediator.Setup(m => m.Send(It.IsAny<SendNotificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ERP.NTF.Application.DTOs.NotificationDto>, CancellationToken>((c, _) => _sent.Add((SendNotificationCommand)c))
            .ReturnsAsync(new ERP.NTF.Application.DTOs.NotificationDto());
    }

    private OrderNotifier Notifier() => new(_mediator.Object, _recipients.Object, NullLogger<OrderNotifier>.Instance);

    private void KnownRecipient(string email = "an@example.com") =>
        _recipients.Setup(r => r.GetOrderRecipientAsync(_orderId, default)).ReturnsAsync(new OrderRecipient
        { OrderId = _orderId, CustomerId = _customerId, OrderNo = "SO-0042", CustomerName = "An Nguyen", Email = email });

    // ─── template rendering ──────────────────────────────────────────────

    [Fact]
    public void Placeholders_are_filled_whatever_their_case_and_the_subject_is_rendered_too()
    {
        var template = NotificationTemplate.Create("ORDER_SHIPPED", "Shipped", NotificationChannel.Email,
            "Order #{OrderNumber} shipped", "Hi {customerName}, tracking {TRACKINGNUMBER}.");
        var vars = new Dictionary<string, string> { ["orderNumber"] = "SO-1", ["customerName"] = "An", ["trackingNumber"] = "TRK9" };

        template.RenderSubject(vars).Should().Be("Order #SO-1 shipped");
        template.Render(vars).Should().Be("Hi An, tracking TRK9.");
    }

    [Fact]
    public void An_unknown_placeholder_is_left_as_it_is()
    {
        NotificationTemplate.Fill("Hi {name}, {other}", new() { ["name"] = "An" }).Should().Be("Hi An, {other}");
    }

    // ─── recipient ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_status_event_goes_to_the_customer_email_remembered_from_the_order()
    {
        KnownRecipient();

        var sent = await Notifier().SendAsync(_orderId, "ORDER_SHIPPED", "k1", new() { ["trackingNumber"] = "TRK1" }, NotificationPriority.Normal, default);

        sent.Should().BeTrue();
        var cmd = _sent.Single();
        cmd.RecipientEmail.Should().Be("an@example.com");
        cmd.RecipientId.Should().Be(_customerId.ToString());   // the customer, not the order id
        cmd.TemplateVariables!["orderNumber"].Should().Be("SO-0042");
        cmd.TemplateVariables["customerName"].Should().Be("An Nguyen");
        cmd.DedupKey.Should().Be("ORDER_SHIPPED:k1");
        cmd.SkipWhenOptedOut.Should().BeTrue();
    }

    [Fact]
    public async Task An_order_with_no_recipient_is_skipped_never_sent_to_a_made_up_address()
    {
        _recipients.Setup(r => r.GetOrderRecipientAsync(_orderId, default)).ReturnsAsync((OrderRecipient?)null);

        var sent = await Notifier().SendAsync(_orderId, "ORDER_DELIVERED", "k", new(), NotificationPriority.Normal, default);

        sent.Should().BeFalse();
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task The_email_on_the_event_is_the_fallback_when_the_order_is_unknown()
    {
        _recipients.Setup(r => r.GetOrderRecipientAsync(_orderId, default)).ReturnsAsync((OrderRecipient?)null);

        await Notifier().SendAsync(_orderId, "PAYMENT_RECEIPT", "k", new(), NotificationPriority.High, default, fallbackEmail: "pay@example.com");

        _sent.Single().RecipientEmail.Should().Be("pay@example.com");
    }

    [Fact]
    public async Task Order_created_remembers_the_recipient_and_sends_the_confirmation_once_per_order()
    {
        KnownRecipient();
        var handler = new SalesOrderCreatedIntegrationEventHandler(_recipients.Object, Notifier());

        await handler.Handle(new SalesOrderCreatedIntegrationEvent
        {
            OrderId = _orderId, CustomerId = _customerId, CustomerName = "An Nguyen", CustomerEmail = "an@example.com",
            OrderNo = "SO-0042", TotalAmount = 1_250_000m, OrderDate = new DateTime(2026, 10, 26)
        }, default);

        _recipients.Verify(r => r.SaveOrderRecipientAsync(
            It.Is<OrderRecipient>(o => o.OrderId == _orderId && o.Email == "an@example.com" && o.OrderNo == "SO-0042"), default), Times.Once);
        var cmd = _sent.Single();
        cmd.TemplateCode.Should().Be("ORDER_CONFIRMATION");
        cmd.DedupKey.Should().Be($"ORDER_CONFIRMATION:{_orderId:N}");
        cmd.TemplateVariables!["totalAmount"].Should().Be("1,250,000");
    }

    [Fact]
    public async Task An_old_order_event_without_an_email_saves_nothing_and_sends_nothing()
    {
        _recipients.Setup(r => r.GetOrderRecipientAsync(_orderId, default)).ReturnsAsync((OrderRecipient?)null);
        var handler = new SalesOrderCreatedIntegrationEventHandler(_recipients.Object, Notifier());

        await handler.Handle(new SalesOrderCreatedIntegrationEvent { OrderId = _orderId, CustomerId = _customerId }, default);

        _recipients.Verify(r => r.SaveOrderRecipientAsync(It.IsAny<OrderRecipient>(), default), Times.Never);
        _sent.Should().BeEmpty();
    }

    // ─── each event picks its template and a key that makes a redelivery a no-op ──

    [Fact]
    public async Task Each_order_and_payment_event_uses_its_own_template_and_dedup_key()
    {
        KnownRecipient();
        var n = Notifier();
        var paymentId = Guid.NewGuid();
        var refundId = Guid.NewGuid();

        await new SalesOrderShippedIntegrationEventHandler(n).Handle(new() { OrderId = _orderId, TrackingNumber = "T1", Carrier = "GHN" }, default);
        await new SalesOrderDeliveredIntegrationEventHandler(n).Handle(new() { OrderId = _orderId, TotalAmount = 500m }, default);
        await new SalesOrderCancelledIntegrationEventHandler(n).Handle(new() { OrderId = _orderId, CancellationReason = "" }, default);
        await new SalesOrderReturnedIntegrationEventHandler(n).Handle(new() { OrderId = _orderId, Reason = "wrong size", RefundAmount = 200m }, default);
        await new PaymentFailedIntegrationEventHandler(n).Handle(new() { OrderId = _orderId, PaymentId = paymentId, Amount = 90m, FailureReason = "declined" }, default);
        await new PaymentRefundedIntegrationEventHandler(n).Handle(new() { OrderId = _orderId, PaymentId = paymentId, RefundId = refundId, RefundAmount = 90m, Currency = "VND" }, default);

        _sent.Select(c => c.TemplateCode).Should().Equal(
            "ORDER_SHIPPED", "ORDER_DELIVERED", "ORDER_CANCELLED", "ORDER_RETURNED", "PAYMENT_FAILED", "PAYMENT_REFUNDED");
        _sent[2].TemplateVariables!["reason"].Should().Be("-");
        _sent[5].DedupKey.Should().Be($"PAYMENT_REFUNDED:{refundId:N}");
        _sent[5].TemplateVariables!["amount"].Should().Be("90 VND");
        _sent.Select(c => c.DedupKey).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Two_partial_refunds_without_ids_are_two_notifications_but_a_replay_is_one()
    {
        KnownRecipient();
        var n = Notifier();
        var paymentId = Guid.NewGuid();
        var h = new PaymentRefundedIntegrationEventHandler(n);

        await h.Handle(new() { OrderId = _orderId, PaymentId = paymentId, RefundAmount = 50m }, default);
        await h.Handle(new() { OrderId = _orderId, PaymentId = paymentId, RefundAmount = 30m }, default);
        await h.Handle(new() { OrderId = _orderId, PaymentId = paymentId, RefundAmount = 50m }, default);

        _sent.Select(c => c.DedupKey).Distinct().Should().HaveCount(2);
    }

    // ─── the send command ────────────────────────────────────────────────

    private (SendNotificationCommandHandler Handler, List<Notification> Added) SendHandler(NotificationTemplate? template, NotificationPreference? preference = null, Notification? existing = null)
    {
        var added = new List<Notification>();
        var notifications = new Mock<INotificationRepository>();
        notifications.Setup(r => r.GetByDedupKeyAsync(It.IsAny<string>(), default)).ReturnsAsync(existing);
        notifications.Setup(r => r.AddAsync(It.IsAny<Notification>(), default)).Callback<Notification, CancellationToken>((n, _) => added.Add(n)).Returns(Task.CompletedTask);
        var templates = new Mock<ITemplateRepository>();
        templates.Setup(t => t.GetByCodeAsync(It.IsAny<string>(), default)).ReturnsAsync(template);
        var preferences = new Mock<IPreferenceRepository>();
        preferences.Setup(p => p.GetByUserIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync(preference);
        var handler = new SendNotificationCommandHandler(notifications.Object, templates.Object, preferences.Object,
            Mock.Of<IEmailSender>(), Mock.Of<ISmsSender>(), Mock.Of<IPushSender>(), Mock.Of<IRealtimeNotifier>());
        return (handler, added);
    }

    [Fact]
    public async Task The_send_command_renders_the_subject_and_the_body_and_keeps_the_dedup_key()
    {
        var template = NotificationTemplate.Create("ORDER_SHIPPED", "x", NotificationChannel.Email, "Order #{orderNumber}", "Tracking {trackingNumber}");
        var (handler, added) = SendHandler(template);

        await handler.Handle(new SendNotificationCommand
        {
            RecipientId = _customerId.ToString(), RecipientEmail = "an@example.com", Channel = NotificationChannel.Email,
            TemplateCode = "ORDER_SHIPPED", TemplateVariables = new() { ["orderNumber"] = "SO-7", ["trackingNumber"] = "T7" },
            Priority = NotificationPriority.Normal, DedupKey = "ORDER_SHIPPED:abc"
        }, default);

        var n = added.Single();
        n.Subject.Should().Be("Order #SO-7");
        n.Body.Should().Be("Tracking T7");
        n.DedupKey.Should().Be("ORDER_SHIPPED:abc");
        n.TemplateId.Should().Be(template.Id);
    }

    [Fact]
    public async Task A_redelivered_event_with_the_same_key_sends_nothing_new()
    {
        var existing = Notification.Create("r", "a@b.c", NotificationChannel.Email, "s", "b", dedupKey: "K");
        var (handler, added) = SendHandler(NotificationTemplate.Create("T", "x", NotificationChannel.Email, "s", "b"), existing: existing);

        var dto = await handler.Handle(new SendNotificationCommand
        {
            RecipientId = _customerId.ToString(), RecipientEmail = "an@example.com", Channel = NotificationChannel.Email,
            TemplateCode = "T", DedupKey = "K"
        }, default);

        added.Should().BeEmpty();
        dto.Id.Should().Be(existing.Id);
    }

    [Fact]
    public async Task A_customer_who_turned_email_off_is_skipped_quietly_by_events_but_refused_for_direct_requests()
    {
        var optedOut = NotificationPreference.CreateDefault(_customerId);
        optedOut.UpdateChannels(false, false, false, true);
        var (handler, added) = SendHandler(NotificationTemplate.Create("T", "x", NotificationChannel.Email, "s", "b"), optedOut);
        var command = new SendNotificationCommand
        {
            RecipientId = _customerId.ToString(), RecipientEmail = "an@example.com", Channel = NotificationChannel.Email, TemplateCode = "T"
        };

        command.SkipWhenOptedOut = true;
        (await handler.Handle(command, default)).Status.Should().Be("Skipped");
        added.Should().BeEmpty();

        command.SkipWhenOptedOut = false;
        await FluentActions.Invoking(() => handler.Handle(command, default)).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task A_recipient_id_that_is_not_a_user_id_has_no_preference_to_check()
    {
        var (handler, added) = SendHandler(NotificationTemplate.Create("T", "x", NotificationChannel.Email, "s", "b"));

        await handler.Handle(new SendNotificationCommand
        {
            RecipientId = "not-a-guid", RecipientEmail = "an@example.com", Channel = NotificationChannel.Email, TemplateCode = "T"
        }, default);

        added.Should().ContainSingle();
    }
}

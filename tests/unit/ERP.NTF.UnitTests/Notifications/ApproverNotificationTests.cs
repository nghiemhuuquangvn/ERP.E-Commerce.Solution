using System.Net;
using ERP.NTF.Application.Commands;
using ERP.NTF.Application.EventHandlers;
using ERP.NTF.Application.Handlers;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Application.Services;
using ERP.NTF.Domain.Entities;
using ERP.NTF.Domain.Enums;
using ERP.NTF.Infrastructure.Services;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.NTF.UnitTests.Notifications;

/// <summary>Approver messages, links, mark-all-read, device tokens and push delivery (Admin app notifications).</summary>
public class ApproverNotificationTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();

    private static (ApproverNotifier Notifier, List<SendNotificationCommand> Sent, Mock<IApproverDirectory> Dir) Notifier(bool push, params Guid[] approvers)
    {
        var sent = new List<SendNotificationCommand>();
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<SendNotificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ERP.NTF.Application.DTOs.NotificationDto>, CancellationToken>((c, _) => sent.Add((SendNotificationCommand)c))
            .ReturnsAsync(new ERP.NTF.Application.DTOs.NotificationDto());
        var dir = new Mock<IApproverDirectory>();
        dir.Setup(d => d.GetUserIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(approvers);
        var status = new Mock<IPushStatus>();
        status.SetupGet(s => s.IsEnabled).Returns(push);
        return (new ApproverNotifier(mediator.Object, dir.Object, status.Object, NullLogger<ApproverNotifier>.Instance), sent, dir);
    }

    [Fact]
    public async Task One_bell_message_per_approver_with_the_link_and_a_distinct_dedup_key()
    {
        var (n, sent, _) = Notifier(false, A, B);
        var id = Guid.NewGuid();
        await new SalesOrderSubmittedIntegrationEventHandler(n).Handle(new SalesOrderSubmittedIntegrationEvent { OrderId = id, OrderNo = "SO-1", TotalAmount = 5 }, default);

        sent.Should().HaveCount(2);
        sent.Should().OnlyContain(c => c.Channel == NotificationChannel.InApp && c.SkipWhenOptedOut && c.EntityType == "SalesOrder" && c.EntityId == id.ToString() && c.RouteKey == "sales-order");
        sent.Select(c => c.DedupKey).Distinct().Should().HaveCount(2);
        sent.Select(c => c.RecipientId).Should().BeEquivalentTo(new[] { A.ToString(), B.ToString() });
    }

    [Fact]
    public async Task A_push_message_is_added_only_when_push_is_switched_on()
    {
        var (on, sentOn, _) = Notifier(true, A);
        await on.NotifyAsync(new ApproverNotifier.Message("p", "E", Guid.NewGuid(), "s", "b", "PurchaseOrder", "purchase-order"), default);
        sentOn.Select(c => c.Channel).Should().BeEquivalentTo(new[] { NotificationChannel.InApp, NotificationChannel.Push });
        sentOn.Select(c => c.DedupKey).Distinct().Should().HaveCount(2);

        var (off, sentOff, _) = Notifier(false, A);
        await off.NotifyAsync(new ApproverNotifier.Message("p", "E", Guid.NewGuid(), "s", "b", "PurchaseOrder", "purchase-order"), default);
        sentOff.Should().ContainSingle();
    }

    [Fact]
    public async Task Nobody_is_told_when_nobody_holds_the_permission_and_a_huge_list_is_capped()
    {
        var (none, sent, _) = Notifier(false);
        (await none.NotifyAsync(new ApproverNotifier.Message("p", "E", Guid.NewGuid(), "s", "b", "x", "y"), default)).Should().Be(0);
        sent.Should().BeEmpty();

        var (many, sentMany, _) = Notifier(false, Enumerable.Range(0, ApproverNotifier.MaxApprovers + 25).Select(_ => Guid.NewGuid()).ToArray());
        await many.NotifyAsync(new ApproverNotifier.Message("p", "E", Guid.NewGuid(), "s", "b", "x", "y"), default);
        sentMany.Should().HaveCount(ApproverNotifier.MaxApprovers);
    }

    [Fact]
    public async Task A_failing_directory_bubbles_so_the_consumer_retries()
    {
        var (n, _, dir) = Notifier(false);
        dir.Setup(d => d.GetUserIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("down"));
        var act = () => n.NotifyAsync(new ApproverNotifier.Message("p", "E", Guid.NewGuid(), "s", "b", "x", "y"), default);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Theory]
    [InlineData("purchase_order:approve", "PurchaseOrder", "purchase-order")]
    [InlineData("purchase_order:finance_approve", "PurchaseOrder", "purchase-order")]
    [InlineData("sales_order:approve", "SalesOrder", "sales-order")]
    [InlineData("hrm:leave:manage", "LeaveRequest", "leave-request")]
    public async Task Each_event_asks_for_its_own_permission_and_links_its_record(string permission, string entity, string route)
    {
        var (n, sent, dir) = Notifier(false, A);
        var id = Guid.NewGuid();
        Task run = permission switch
        {
            "purchase_order:approve" => new PurchaseOrderSubmittedIntegrationEventHandler(n).Handle(new PurchaseOrderSubmittedIntegrationEvent { PurchaseOrderId = id, PONumber = "PO-1" }, default),
            "purchase_order:finance_approve" => new PurchaseOrderPendingFinanceApprovalIntegrationEventHandler(n).Handle(new PurchaseOrderPendingFinanceApprovalIntegrationEvent { PurchaseOrderId = id, PONumber = "PO-1" }, default),
            "sales_order:approve" => new SalesOrderSubmittedIntegrationEventHandler(n).Handle(new SalesOrderSubmittedIntegrationEvent { OrderId = id, OrderNo = "SO-1" }, default),
            _ => new LeaveRequestSubmittedIntegrationEventHandler(n).Handle(new LeaveRequestSubmittedIntegrationEvent { LeaveRequestId = id, LeaveRequestNumber = "LR-1", DaysRequested = 1, StartDate = DateTime.Today, EndDate = DateTime.Today }, default),
        };
        await run;

        dir.Verify(d => d.GetUserIdsAsync(permission, It.IsAny<CancellationToken>()), Times.Once);
        sent.Should().ContainSingle().Which.Should().Match<SendNotificationCommand>(c => c.EntityType == entity && c.RouteKey == route && c.EntityId == id.ToString());
    }

    [Fact]
    public void A_notification_keeps_its_link()
    {
        var n = Notification.Create("u", "", NotificationChannel.InApp, "s", "b", entityType: "PurchaseOrder", entityId: "1", routeKey: "purchase-order");
        (n.EntityType, n.EntityId, n.RouteKey).Should().Be(("PurchaseOrder", "1", "purchase-order"));
    }

    [Fact]
    public async Task Mark_all_read_marks_only_the_callers_own_recipient_id_and_ignores_a_blank_one()
    {
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.MarkAllReadAsync("me", It.IsAny<CancellationToken>())).ReturnsAsync(3);
        var h = new MarkAllNotificationsReadCommandHandler(repo.Object);

        (await h.Handle(new MarkAllNotificationsReadCommand { RecipientId = "me" }, default)).Should().Be(3);
        (await h.Handle(new MarkAllNotificationsReadCommand { RecipientId = " " }, default)).Should().Be(0);
        repo.Verify(r => r.MarkAllReadAsync(It.Is<string>(s => s != "me"), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── device tokens ──
    private static readonly Guid U = Guid.NewGuid();

    [Theory]
    [InlineData("", "d")]
    [InlineData("t", "")]
    public async Task A_blank_token_or_device_is_refused(string token, string device)
    {
        var h = new RegisterDeviceTokenCommandHandler(Mock.Of<IDeviceTokenRepository>());
        var act = () => h.Handle(new RegisterDeviceTokenCommand { UserId = U, Token = token, DeviceId = device }, default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task A_new_device_is_added_the_same_device_is_replaced_and_a_token_held_by_another_row_moves()
    {
        var repo = new Mock<IDeviceTokenRepository>();
        var h = new RegisterDeviceTokenCommandHandler(repo.Object);

        await h.Handle(new RegisterDeviceTokenCommand { UserId = U, Token = "t1", DeviceId = "d1", Platform = "weird" }, default);
        repo.Verify(r => r.AddAsync(It.Is<DeviceToken>(d => d.Token == "t1" && d.Platform == "android"), It.IsAny<CancellationToken>()), Times.Once);

        var mine = DeviceToken.Create(U, "old", "d1", "android");
        repo.Setup(r => r.GetByDeviceAsync(U, "d1", It.IsAny<CancellationToken>())).ReturnsAsync(mine);
        var other = DeviceToken.Create(Guid.NewGuid(), "t2", "dx", "android");
        repo.Setup(r => r.GetByTokenAsync("t2", It.IsAny<CancellationToken>())).ReturnsAsync(other);
        await h.Handle(new RegisterDeviceTokenCommand { UserId = U, Token = "t2", DeviceId = "d1", Platform = "ios" }, default);

        repo.Verify(r => r.RemoveAsync(other, It.IsAny<CancellationToken>()), Times.Once);
        mine.Token.Should().Be("t2");
        mine.Platform.Should().Be("ios");
        repo.Verify(r => r.UpdateAsync(mine, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unregister_removes_the_row_and_tells_whether_there_was_one()
    {
        var repo = new Mock<IDeviceTokenRepository>();
        var row = DeviceToken.Create(U, "t", "d", "android");
        repo.Setup(r => r.GetByDeviceAsync(U, "d", It.IsAny<CancellationToken>())).ReturnsAsync(row);
        var h = new UnregisterDeviceTokenCommandHandler(repo.Object);

        (await h.Handle(new UnregisterDeviceTokenCommand { UserId = U, DeviceId = "d" }, default)).Should().BeTrue();
        (await h.Handle(new UnregisterDeviceTokenCommand { UserId = U, DeviceId = "zz" }, default)).Should().BeFalse();
        repo.Verify(r => r.RemoveAsync(row, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── push delivery ──
    private static Notification PushRow(string recipient, string? token = null) =>
        Notification.Create(recipient, "", NotificationChannel.Push, "s", "b", recipientDeviceToken: token, entityType: "SalesOrder", entityId: "9", routeKey: "sales-order");

    [Fact]
    public async Task Push_goes_to_every_device_of_the_user_with_the_link_as_data()
    {
        var repo = new Mock<IDeviceTokenRepository>();
        repo.Setup(r => r.GetByUserAsync(U, It.IsAny<CancellationToken>())).ReturnsAsync(new List<DeviceToken> { DeviceToken.Create(U, "t1", "d1", "android"), DeviceToken.Create(U, "t2", "d2", "android") });
        var sender = new Mock<IPushSender>();
        var delivery = new PushDelivery(repo.Object, sender.Object, NullLogger<PushDelivery>.Instance);

        (await delivery.DeliverAsync(PushRow(U.ToString()), default)).Should().Be(2);
        sender.Verify(s => s.SendAsync(It.IsAny<string>(), "s", "b", It.IsAny<CancellationToken>(), It.Is<IReadOnlyDictionary<string, string>>(d => d["entityType"] == "SalesOrder" && d["entityId"] == "9" && d["routeKey"] == "sales-order")), Times.Exactly(2));
    }

    [Fact]
    public async Task A_dead_token_is_forgotten_and_one_broken_device_does_not_stop_the_others()
    {
        var dead = DeviceToken.Create(U, "dead", "d1", "android");
        var repo = new Mock<IDeviceTokenRepository>();
        repo.Setup(r => r.GetByUserAsync(U, It.IsAny<CancellationToken>())).ReturnsAsync(new List<DeviceToken> { dead, DeviceToken.Create(U, "bad", "d2", "android"), DeviceToken.Create(U, "ok", "d3", "android") });
        var sender = new Mock<IPushSender>();
        sender.Setup(s => s.SendAsync("dead", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyDictionary<string, string>?>())).ThrowsAsync(new InvalidPushTokenException("gone"));
        sender.Setup(s => s.SendAsync("bad", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyDictionary<string, string>?>())).ThrowsAsync(new HttpRequestException("x"));
        var delivery = new PushDelivery(repo.Object, sender.Object, NullLogger<PushDelivery>.Instance);

        (await delivery.DeliverAsync(PushRow(U.ToString()), default)).Should().Be(1);
        repo.Verify(r => r.RemoveAsync(dead, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Push_fails_only_when_no_device_could_be_reached_and_no_device_is_not_a_failure()
    {
        var repo = new Mock<IDeviceTokenRepository>();
        repo.Setup(r => r.GetByUserAsync(U, It.IsAny<CancellationToken>())).ReturnsAsync(new List<DeviceToken>());
        var sender = new Mock<IPushSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyDictionary<string, string>?>())).ThrowsAsync(new HttpRequestException("x"));
        var delivery = new PushDelivery(repo.Object, sender.Object, NullLogger<PushDelivery>.Instance);

        (await delivery.DeliverAsync(PushRow(U.ToString()), default)).Should().Be(0);
        var act = () => delivery.DeliverAsync(PushRow("x", "one-token"), default);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── FCM v1 sender and the approver directory over a stub HTTP handler ──
    private sealed class Stub : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _reply;
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = new();
        public Stub(Func<HttpRequestMessage, HttpResponseMessage> reply) => _reply = reply;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add((request, request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct)));
            return _reply(request);
        }
    }

    private static FcmV1PushSender Fcm(Stub stub, Mock<IFcmAccessTokenProvider>? tokens = null)
    {
        tokens ??= new Mock<IFcmAccessTokenProvider>();
        tokens.Setup(t => t.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync("tok");
        return new FcmV1PushSender(new HttpClient(stub) { BaseAddress = new Uri("https://fcm.test") }, tokens.Object, new FcmOptions { ProjectId = "proj", ServiceAccountPath = "x" }, NullLogger<FcmV1PushSender>.Instance);
    }

    [Fact]
    public async Task Fcm_posts_to_the_v1_endpoint_with_a_bearer_token_and_the_data()
    {
        var stub = new Stub(_ => new HttpResponseMessage(HttpStatusCode.OK));
        await Fcm(stub).SendAsync("dev", "T", "B", default, new Dictionary<string, string> { ["entityId"] = "9" });

        var (req, body) = stub.Calls.Single();
        req.RequestUri!.ToString().Should().Be("https://fcm.test/v1/projects/proj/messages:send");
        req.Headers.Authorization!.ToString().Should().Be("Bearer tok");
        body.Should().Contain("\"token\":\"dev\"").And.Contain("\"entityId\":\"9\"");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "{}")]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":{\"status\":\"INVALID_ARGUMENT\",\"message\":\"The registration token is not valid\"}}")]
    [InlineData(HttpStatusCode.OK, "UNREGISTERED")]
    public async Task Fcm_reports_a_token_it_does_not_know_as_invalid(HttpStatusCode code, string text)
    {
        if (code == HttpStatusCode.OK) code = HttpStatusCode.Forbidden; // an UNREGISTERED body is judged whatever the status
        var stub = new Stub(_ => new HttpResponseMessage(code) { Content = new StringContent(text) });
        var act = () => Fcm(stub).SendAsync("dev", "T", "B");
        await act.Should().ThrowAsync<InvalidPushTokenException>();
    }

    [Fact]
    public async Task Fcm_401_drops_the_cached_access_token_and_fails_so_it_is_retried()
    {
        var tokens = new Mock<IFcmAccessTokenProvider>();
        var stub = new Stub(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var act = () => Fcm(stub, tokens).SendAsync("dev", "T", "B");
        await act.Should().ThrowAsync<HttpRequestException>();
        tokens.Verify(t => t.Invalidate(), Times.Once);
    }

    [Fact]
    public void Push_is_configured_only_with_both_a_project_and_a_service_account()
    {
        new FcmOptions().IsConfigured.Should().BeFalse();
        new FcmOptions { ProjectId = "p" }.IsConfigured.Should().BeFalse();
        new FcmOptions { ProjectId = "p", ServiceAccountPath = "f.json" }.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task The_directory_asks_identity_once_per_minute_per_tenant_and_permission_and_needs_a_tenant()
    {
        var stub = new Stub(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{{\"permission\":\"p\",\"userIds\":[\"{A}\"]}}", System.Text.Encoding.UTF8, "application/json") });
        var dir = new ApproverDirectory(new HttpClient(stub) { BaseAddress = new Uri("http://identity/") }, new MemoryCache(new MemoryCacheOptions()));

        var noTenant = () => dir.GetUserIdsAsync("p");
        await noTenant.Should().ThrowAsync<HttpRequestException>();

        using (ERP.Shared.Infrastructure.Data.TenantScope.Begin(Guid.NewGuid()))
        {
            (await dir.GetUserIdsAsync("p")).Should().Equal(A);
            (await dir.GetUserIdsAsync("p")).Should().Equal(A);
        }
        stub.Calls.Should().ContainSingle();
        stub.Calls[0].Request.RequestUri!.ToString().Should().Be("http://identity/internal/v1/permissions/p/users");
    }
}

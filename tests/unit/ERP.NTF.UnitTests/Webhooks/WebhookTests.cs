using System.Net;
using ERP.NTF.Application.Commands;
using ERP.NTF.Application.Handlers;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Application.Queries;
using ERP.NTF.Application.Webhooks;
using ERP.NTF.Domain.Entities;
using ERP.NTF.Infrastructure.Services;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.NTF.UnitTests.Webhooks;

/// <summary>ROADMAP P2-1b: webhook endpoints keep their secret safe, refuse internal addresses, can be tested and leave a delivery log.</summary>
public class WebhookTests
{
    /// <summary>Not real crypto: it makes "sealed" visible in the assertions.</summary>
    private sealed class FakeProtector : ISecretProtector
    {
        public string Protect(string plaintext) => "P(" + plaintext + ")";
        public string Unprotect(string protectedValue) => protectedValue[2..^1];
    }

    private readonly Mock<IWebhookRepository> _repo = new();
    private readonly FakeProtector _protector = new();
    private readonly List<WebhookDelivery> _log = new();

    public WebhookTests()
    {
        _repo.Setup(r => r.AddDeliveryAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()))
            .Callback<WebhookDelivery, CancellationToken>((d, _) => _log.Add(d)).Returns(Task.CompletedTask);
    }

    private RegisterWebhookCommandHandler Register() => new(_repo.Object, _protector);
    private UpdateWebhookCommandHandler Update() => new(_repo.Object, _protector);

    private static RegisterWebhookCommand Command(string url = "https://hooks.example.com/erp", string? secret = "s3cret", params string[] events) =>
        new() { Name = "Shop hook", Url = url, Secret = secret, EventTypes = events.ToList() };

    // ── register / update ─────────────────────────────────────────────────

    [Fact]
    public async Task The_secret_is_stored_sealed_and_never_returned()
    {
        WebhookEndpoint? saved = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<WebhookEndpoint>(), It.IsAny<CancellationToken>())).Callback<WebhookEndpoint, CancellationToken>((w, _) => saved = w).Returns(Task.CompletedTask);

        var dto = await Register().Handle(Command(), default);

        saved!.Secret.Should().Be("enc:v1:P(s3cret)");
        dto.HasSecret.Should().BeTrue();
        dto.GetType().GetProperties().Select(p => p.Name).Should().NotContain("Secret");
    }

    [Theory]
    [InlineData("http://hooks.example.com/x")]            // not https
    [InlineData("https://localhost/x")]
    [InlineData("https://127.0.0.1/x")]
    [InlineData("https://10.1.2.3/x")]
    [InlineData("https://192.168.0.5/x")]
    [InlineData("https://169.254.169.254/latest/meta-data")]   // cloud metadata
    [InlineData("https://sqlserver/x")]                    // a Docker service name
    [InlineData("https://user:pw@hooks.example.com/x")]    // credentials in the address
    [InlineData("not a url")]
    public async Task An_internal_or_unsafe_address_is_refused(string url)
    {
        var act = () => Register().Handle(Command(url), default);
        await act.Should().ThrowAsync<BusinessRuleException>();
        _repo.Verify(r => r.AddAsync(It.IsAny<WebhookEndpoint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Only_known_events_can_be_subscribed_and_a_name_is_required()
    {
        (await ((Func<Task>)(() => Register().Handle(Command(events: "order.created"), default))).Should().ThrowAsync<BusinessRuleException>()).Which.Message.Should().Contain("order.created");
        await ((Func<Task>)(() => Register().Handle(new RegisterWebhookCommand { Name = " ", Url = "https://hooks.example.com/x" }, default))).Should().ThrowAsync<BusinessRuleException>();

        var ok = await Register().Handle(Command(events: new[] { "notification.sent", "notification.failed", "notification.sent" }), default);
        ok.EventTypes.Should().Equal("notification.sent", "notification.failed");
    }

    private WebhookEndpoint Existing(string? sealedSecret = "enc:v1:P(old)")
    {
        var w = WebhookEndpoint.Create("Shop hook", "https://hooks.example.com/erp", sealedSecret, new[] { "notification.sent" });
        _repo.Setup(r => r.GetByIdAsync(w.Id, It.IsAny<CancellationToken>())).ReturnsAsync(w);
        return w;
    }

    [Fact]
    public async Task An_edit_without_a_secret_keeps_the_stored_one()
    {
        var w = Existing();
        await Update().Handle(new UpdateWebhookCommand { WebhookId = w.Id, Name = "Renamed", Url = "https://hooks.example.com/new", EventTypes = new() }, default);

        w.Secret.Should().Be("enc:v1:P(old)");
        w.Name.Should().Be("Renamed");
        w.Url.Should().Be("https://hooks.example.com/new");
        w.EventTypes.Should().BeEmpty();      // empty = all events
    }

    [Fact]
    public async Task A_new_secret_replaces_it_and_clear_removes_it()
    {
        var w = Existing();
        await Update().Handle(new UpdateWebhookCommand { WebhookId = w.Id, Name = "n", Url = "https://hooks.example.com/x", Secret = "fresh" }, default);
        w.Secret.Should().Be("enc:v1:P(fresh)");

        await Update().Handle(new UpdateWebhookCommand { WebhookId = w.Id, Name = "n", Url = "https://hooks.example.com/x", ClearSecret = true }, default);
        w.HasSecret.Should().BeFalse();
    }

    [Fact]
    public async Task A_secret_saved_in_plaintext_before_P2_1b_is_sealed_on_its_next_save()
    {
        var w = Existing(sealedSecret: "plain-old-secret");
        await Update().Handle(new UpdateWebhookCommand { WebhookId = w.Id, Name = "n", Url = "https://hooks.example.com/x" }, default);
        w.Secret.Should().Be("enc:v1:P(plain-old-secret)");
    }

    [Fact]
    public async Task An_unknown_endpoint_is_a_not_found_for_update_delete_test_and_deliveries()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await ((Func<Task>)(() => Update().Handle(new UpdateWebhookCommand { WebhookId = id, Name = "n", Url = "https://hooks.example.com/x" }, default))).Should().ThrowAsync<KeyNotFoundException>();
        await ((Func<Task>)(() => new DeleteWebhookCommandHandler(_repo.Object).Handle(new DeleteWebhookCommand { WebhookId = id }, default))).Should().ThrowAsync<KeyNotFoundException>();
        await ((Func<Task>)(() => new TestWebhookCommandHandler(_repo.Object, Mock.Of<IWebhookSender>()).Handle(new TestWebhookCommand { WebhookId = id }, default))).Should().ThrowAsync<KeyNotFoundException>();
        await ((Func<Task>)(() => new ListWebhookDeliveriesQueryHandler(_repo.Object).Handle(new ListWebhookDeliveriesQuery { WebhookId = id }, default))).Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task The_list_carries_the_last_delivery_of_each_endpoint()
    {
        var a = Existing();
        var b = Existing();
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<WebhookEndpoint> { a, b });
        _repo.Setup(r => r.GetLastDeliveriesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WebhookDelivery> { [a.Id] = WebhookDelivery.Create(a.Id, "notification.sent", false, 500, "HTTP 500", 120) });

        var list = await new ListWebhooksQueryHandler(_repo.Object).Handle(new ListWebhooksQuery(), default);

        list.Single(w => w.Id == a.Id).LastDelivery!.StatusCode.Should().Be(500);
        list.Single(w => w.Id == b.Id).LastDelivery.Should().BeNull();
    }

    [Fact]
    public async Task The_deliveries_are_paged_and_the_page_size_is_capped()
    {
        var w = Existing();
        _repo.Setup(r => r.ListDeliveriesAsync(w.Id, 100, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<WebhookDelivery> { WebhookDelivery.Create(w.Id, "webhook.test", true, 200, null, 80) }, 101));

        var page = await new ListWebhookDeliveriesQueryHandler(_repo.Object).Handle(new ListWebhookDeliveriesQuery { WebhookId = w.Id, Page = 2, PageSize = 5000 }, default);

        page.TotalCount.Should().Be(101);
        page.PageSize.Should().Be(100);
        page.Items.Should().ContainSingle().Which.EventType.Should().Be("webhook.test");
    }

    [Fact]
    public async Task The_event_catalog_lists_what_can_be_subscribed_to()
    {
        var types = await new GetWebhookEventTypesQueryHandler().Handle(new GetWebhookEventTypesQuery(), default);
        types.Select(t => t.Type).Should().Equal("notification.sent", "notification.failed");
    }

    // ── the sender ────────────────────────────────────────────────────────

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpStatusCode Answer { get; set; } = HttpStatusCode.OK;
        public Exception? Throw { get; set; }
        public HttpRequestMessage? Seen { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;
            Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            if (Throw != null) throw Throw;
            return new HttpResponseMessage(Answer);
        }
    }

    private sealed class StubFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private (WebhookSender Sender, StubHandler Http) Sender()
    {
        var http = new StubHandler();
        return (new WebhookSender(new StubFactory(http), _protector, _repo.Object, NullLogger<WebhookSender>.Instance), http);
    }

    [Fact]
    public async Task A_delivery_is_signed_with_the_opened_secret_and_logged()
    {
        var (sender, http) = Sender();
        var w = WebhookEndpoint.Create("hook", "https://hooks.example.com/erp", "enc:v1:P(s3cret)", Array.Empty<string>());

        var result = await sender.SendAsync(w, "notification.sent", new { Id = 1 }, default);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        http.Seen!.Headers.GetValues("X-Event-Type").Should().ContainSingle("notification.sent");
        var timestamp = http.Seen.Headers.GetValues("X-Timestamp").Single();
        var expected = Convert.ToHexString(new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes("s3cret"))
            .ComputeHash(System.Text.Encoding.UTF8.GetBytes($"{timestamp}.{http.Body}"))).ToLowerInvariant();
        http.Seen.Headers.GetValues("X-Hub-Signature").Should().ContainSingle($"sha256={expected}");
        _log.Should().ContainSingle().Which.Success.Should().BeTrue();
    }

    [Fact]
    public async Task A_failing_endpoint_is_a_logged_failure_not_an_exception()
    {
        var (sender, http) = Sender();
        var w = WebhookEndpoint.Create("hook", "https://hooks.example.com/erp", null, Array.Empty<string>());

        http.Answer = HttpStatusCode.InternalServerError;
        var bad = await sender.SendAsync(w, "notification.failed", new { }, default);
        bad.Success.Should().BeFalse();
        bad.StatusCode.Should().Be(500);
        bad.Error.Should().Contain("500");

        http.Throw = new HttpRequestException("name not resolved");
        var down = await sender.SendAsync(w, "notification.failed", new { }, default);
        down.Success.Should().BeFalse();
        down.StatusCode.Should().BeNull();
        down.Error.Should().Contain("name not resolved");

        _log.Should().HaveCount(2).And.OnlyContain(d => !d.Success);
        http.Seen!.Headers.Contains("X-Hub-Signature").Should().BeFalse();   // no secret, no signature
    }

    [Fact]
    public async Task An_endpoint_saved_with_a_now_forbidden_address_is_never_called()
    {
        var (sender, http) = Sender();
        var w = WebhookEndpoint.Create("old", "http://10.0.0.7/internal", null, Array.Empty<string>());   // saved before the rule existed

        var result = await sender.SendAsync(w, "notification.sent", new { }, default);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not allowed");
        http.Seen.Should().BeNull();
        _log.Should().ContainSingle().Which.Error.Should().Contain("not allowed");
    }

    [Fact]
    public async Task The_test_button_sends_a_test_event_and_reports_the_answer()
    {
        var w = Existing(null);
        var (sender, http) = Sender();
        http.Answer = HttpStatusCode.Accepted;

        var result = await new TestWebhookCommandHandler(_repo.Object, sender).Handle(new TestWebhookCommand { WebhookId = w.Id }, default);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(202);
        http.Seen!.Headers.GetValues("X-Event-Type").Should().ContainSingle("webhook.test");
        _log.Should().ContainSingle().Which.EventType.Should().Be("webhook.test");
    }
}

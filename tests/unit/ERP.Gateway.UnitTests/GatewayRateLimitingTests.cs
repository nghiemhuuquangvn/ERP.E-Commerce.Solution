using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Ocelot.API.RateLimiting;
using Xunit;

namespace ERP.Gateway.UnitTests;

/// <summary>ROADMAP P0-8: coarse per-client-IP limit on the sign-in / token / chat endpoints.</summary>
public class GatewayRateLimitingTests
{
    private static readonly string[] Trusted = ["172.16.0.0/12", "10.0.0.0/8"];

    private static GatewayRateLimiter NewLimiter(int auth = 3, int chat = 2, bool enabled = true) =>
        new(new GatewayRateLimitOptions { AuthPermitPerMinute = auth, ChatPermitPerMinute = chat, Enabled = enabled }, Trusted);

    private static DefaultHttpContext Request(string method, string path, string peer = "172.18.0.5", string? forwardedFor = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Response.Body = new MemoryStream();
        if (forwardedFor != null)
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        return context;
    }

    /// <summary>Runs one request through the middleware; true when it reached the next handler.</summary>
    private static async Task<(bool Passed, int Status)> SendAsync(GatewayRateLimiter limiter, DefaultHttpContext context)
    {
        var passed = false;
        var middleware = new GatewayRateLimitMiddleware(_ => { passed = true; return Task.CompletedTask; }, limiter, NullLogger<GatewayRateLimitMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        return (passed, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/v1/auth/login")]
    [InlineData("POST", "/api/v1/auth/2fa/verify")]
    [InlineData("POST", "/api/v1/auth/2fa/resend")]
    [InlineData("POST", "/api/v1/auth/forgot-password")]
    [InlineData("POST", "/api/v1/auth/reset-password")]
    [InlineData("POST", "/api/v1/auth/register")]
    [InlineData("POST", "/api/v1/auth/tenant-signup")]
    [InlineData("POST", "/api/v1/auth/refresh")]
    [InlineData("POST", "/api/v1/auth/external-login")]
    [InlineData("POST", "/api/v1/auth/external-login/twitter/callback")]
    [InlineData("POST", "/id/api/v1/auth/login")]
    [InlineData("POST", "/connect/token")]
    [InlineData("POST", "/API/V1/AUTH/LOGIN")]
    public void SignInAndTokenEndpoints_CountAgainstTheAuthLimit(string method, string path) =>
        GatewayRateLimiter.Classify(Request(method, path).Request).Should().Be(RateLimitClass.Auth);

    [Theory]
    [InlineData("GET", "/api/v1/auth/login-options")]
    [InlineData("GET", "/api/v1/products")]
    [InlineData("GET", "/api/v1/orders")]
    [InlineData("POST", "/api/v1/orders")]
    [InlineData("GET", "/.well-known/openid-configuration")]
    [InlineData("OPTIONS", "/api/v1/auth/login")]
    [InlineData("GET", "/api/v1/chatbot/conversations")]
    public void OtherRequests_AreNotLimited(string method, string path) =>
        GatewayRateLimiter.Classify(Request(method, path).Request).Should().BeNull();

    [Fact]
    public void ChatMessagesAndConversationStarts_HaveTheirOwnLimit() =>
        GatewayRateLimiter.Classify(Request("POST", "/api/v1/chatbot/conversations").Request).Should().Be(RateLimitClass.Chat);

    [Fact]
    public async Task TheRequestAfterTheLimit_Gets429WithRetryAfter_AndNeverReachesTheService()
    {
        using var limiter = NewLimiter(auth: 3);
        for (var i = 0; i < 3; i++)
            (await SendAsync(limiter, Request("POST", "/api/v1/auth/login", forwardedFor: "203.0.113.7"))).Passed.Should().BeTrue();

        var blocked = Request("POST", "/api/v1/auth/login", forwardedFor: "203.0.113.7");
        var (passed, status) = await SendAsync(limiter, blocked);

        passed.Should().BeFalse();
        status.Should().Be(StatusCodes.Status429TooManyRequests);
        blocked.Response.Headers.RetryAfter.ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AnotherClient_IsNotAffected_ByTheFirstOnesLimit()
    {
        using var limiter = NewLimiter(auth: 2);
        for (var i = 0; i < 3; i++)
            await SendAsync(limiter, Request("POST", "/api/v1/auth/login", forwardedFor: "203.0.113.7"));

        (await SendAsync(limiter, Request("POST", "/api/v1/auth/login", forwardedFor: "203.0.113.8"))).Passed.Should().BeTrue();
    }

    [Fact]
    public async Task TheAuthAndChatLimits_AreCountedSeparately()
    {
        using var limiter = NewLimiter(auth: 1, chat: 1);
        await SendAsync(limiter, Request("POST", "/api/v1/auth/login", forwardedFor: "203.0.113.7"));

        (await SendAsync(limiter, Request("POST", "/api/v1/chatbot/conversations", forwardedFor: "203.0.113.7"))).Passed.Should().BeTrue();
    }

    [Fact]
    public async Task UnlimitedRoutes_AreNeverBlocked()
    {
        using var limiter = NewLimiter(auth: 1);
        for (var i = 0; i < 20; i++)
            (await SendAsync(limiter, Request("GET", "/api/v1/products", forwardedFor: "203.0.113.7"))).Passed.Should().BeTrue();
    }

    [Fact]
    public async Task WhenDisabled_NothingIsBlocked()
    {
        using var limiter = NewLimiter(auth: 1, enabled: false);
        for (var i = 0; i < 5; i++)
            (await SendAsync(limiter, Request("POST", "/api/v1/auth/login", forwardedFor: "203.0.113.7"))).Passed.Should().BeTrue();
    }

    // ---- which address is "the client" ----

    [Fact]
    public void ClientIp_IsTheFirstAddressFromTheRight_ThatIsNotATrustedProxy()
    {
        using var limiter = NewLimiter();

        // client → nginx → SPA nginx → Gateway: "client, nginx" with the SPA nginx as the TCP peer.
        limiter.ClientIp(Request("POST", "/x", peer: "172.18.0.9", forwardedFor: "203.0.113.7, 172.18.0.4")).Should().Be("203.0.113.7");
    }

    [Fact]
    public void ClientIp_IgnoresWhatTheClientPutOnTheLeftOfTheHeader()
    {
        using var limiter = NewLimiter();

        limiter.ClientIp(Request("POST", "/x", peer: "172.18.0.9", forwardedFor: "1.2.3.4, 198.51.100.20, 172.18.0.4")).Should().Be("198.51.100.20");
    }

    [Fact]
    public void ClientIp_FromAnUntrustedPeer_IgnoresTheHeaderAltogether()
    {
        using var limiter = NewLimiter();

        limiter.ClientIp(Request("POST", "/x", peer: "198.51.100.99", forwardedFor: "1.2.3.4")).Should().Be("198.51.100.99");
    }

    [Fact]
    public void ClientIp_WhenEveryHopIsTrusted_IsTheLeftmost()
    {
        using var limiter = NewLimiter();

        // local run: the browser reaches nginx as the Docker bridge address, which is inside a trusted range
        limiter.ClientIp(Request("POST", "/x", peer: "172.18.0.9", forwardedFor: "172.18.0.1, 172.18.0.4")).Should().Be("172.18.0.1");
    }

    [Fact]
    public async Task ASpoofedHeader_CannotBuyAFreshBucket()
    {
        using var limiter = NewLimiter(auth: 2);
        for (var i = 0; i < 2; i++)
            await SendAsync(limiter, Request("POST", "/api/v1/auth/login", forwardedFor: $"10.9.9.{i}, 203.0.113.7, 172.18.0.4"));

        var (passed, _) = await SendAsync(limiter, Request("POST", "/api/v1/auth/login", forwardedFor: "10.9.9.77, 203.0.113.7, 172.18.0.4"));

        passed.Should().BeFalse("rotating the client-controlled left side of X-Forwarded-For must not reset the counter");
    }

    [Fact]
    public void ClientIp_MalformedHeader_FallsBackToThePeer()
    {
        using var limiter = NewLimiter();

        limiter.ClientIp(Request("POST", "/x", peer: "172.18.0.9", forwardedFor: "not-an-ip")).Should().Be("172.18.0.9");
    }
}

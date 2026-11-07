using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using ERP.Shared.Streaming;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace ERP.Shared.UnitTests.Streaming;

/// <summary>The Gateway's streaming pass-through: nothing is buffered in either direction, and the Gateway's own checks stay in front of it.</summary>
public class StreamingProxyTests : IAsyncLifetime
{
    private IHost _downstreamHost = null!;
    private IHost _proxyHost = null!;
    private HttpClient _client = null!;
    private readonly List<(string Method, string PathAndQuery, IHeaderDictionary Headers)> _seen = new();
    private bool _serviceDown;

    /// <summary>The route to the service, which can be switched to "connection refused".</summary>
    private sealed class SwitchableHandler : DelegatingHandler
    {
        private readonly StreamingProxyTests _owner;
        public SwitchableHandler(StreamingProxyTests owner, HttpMessageHandler inner) : base(inner) => _owner = owner;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _owner._serviceDown ? throw new HttpRequestException("connection refused") : base.SendAsync(request, cancellationToken);
    }

    public async Task InitializeAsync()
    {
        _downstreamHost = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer().Configure(app => app.Run(async context =>
        {
            _seen.Add((context.Request.Method, context.Request.Path + context.Request.QueryString.Value, context.Request.Headers));
            switch (context.Request.Path.Value)
            {
                case "/api/v1/products/import":   // echoes every request line back at once
                    context.Response.ContentType = "application/x-ndjson";
                    using (var reader = new StreamReader(context.Request.Body))
                    {
                        string? line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            await context.Response.WriteAsync("echo:" + line + "\n");
                            await context.Response.Body.FlushAsync();
                        }
                    }
                    break;
                case "/api/v1/products/import/abc/events":   // an event now, another one a moment later
                    context.Response.ContentType = "text/event-stream";
                    await context.Response.WriteAsync("event: progress\ndata: 1\n\n");
                    await context.Response.Body.FlushAsync();
                    await Task.Delay(400);
                    await context.Response.WriteAsync("event: completed\ndata: 2\n\n");
                    break;
                case "/api/v1/products/import/refused":
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("{\"detail\":\"bad header\"}");
                    break;
                default:
                    context.Response.StatusCode = 404;
                    break;
            }
        }))).StartAsync();

        _proxyHost = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
            .ConfigureServices(services =>
            {
                services.Configure<StreamingProxyOptions>(o => o.Routes.Add(new StreamingProxyRoute
                {
                    UpstreamPrefix = "/prd/api/v1/products/import",
                    DownstreamBase = "http://downstream/api/v1/products/import",
                }));
                services.AddHttpClient(StreamingProxyMiddleware.HttpClientName, c => c.Timeout = Timeout.InfiniteTimeSpan)
                    .ConfigurePrimaryHttpMessageHandler(() => new SwitchableHandler(this, _downstreamHost.GetTestServer().CreateHandler()));
            })
            .Configure(app =>
            {
                // stands in for the Gateway's authentication + tenant check: a header makes the caller "signed in"
                app.Use(async (context, next) =>
                {
                    if (context.Request.Headers.ContainsKey("X-Test-Auth"))
                        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "u1") }, "test"));
                    await next();
                });
                app.UseStreamingProxy();
                app.Run(async context => await context.Response.WriteAsync("next"));
            })).StartAsync();

        _client = _proxyHost.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _proxyHost.StopAsync();
        await _downstreamHost.StopAsync();
    }

    private HttpRequestMessage Signed(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Test-Auth", "1");
        return request;
    }

    [Fact]
    public async Task An_unauthenticated_call_is_refused_and_never_reaches_the_service()
    {
        var response = await _client.PostAsync("/prd/api/v1/products/import", new StringContent("a\n"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _seen.Should().BeEmpty();
    }

    [Fact]
    public async Task Other_paths_and_look_alike_prefixes_go_on_to_the_next_middleware()
    {
        (await _client.GetStringAsync("/prd/api/v1/products")).Should().Be("next");
        (await _client.GetStringAsync("/prd/api/v1/products/import-all")).Should().Be("next");
        _seen.Should().BeEmpty();
    }

    [Fact]
    public async Task The_path_rest_the_query_and_the_gateway_headers_are_forwarded_and_hop_by_hop_headers_are_not()
    {
        var request = Signed(HttpMethod.Get, "/prd/api/v1/products/import/abc/events?x=1");
        request.Headers.Add("Authorization", "Bearer token-1");
        request.Headers.Add("X-Tenant-Id", "tenant-1");
        request.Headers.Add("X-User-Id", "user-1");
        request.Headers.TryAddWithoutValidation("Keep-Alive", "timeout=5");

        var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        await response.Content.ReadAsStringAsync();

        var seen = _seen.Single();
        seen.PathAndQuery.Should().Be("/api/v1/products/import/abc/events?x=1");
        seen.Headers["Authorization"].ToString().Should().Be("Bearer token-1");
        seen.Headers["X-Tenant-Id"].ToString().Should().Be("tenant-1");
        seen.Headers["X-User-Id"].ToString().Should().Be("user-1");
        seen.Headers.ContainsKey("Keep-Alive").Should().BeFalse();
        response.Headers.GetValues("X-Accel-Buffering").Should().Equal("no");
    }

    [Fact]
    public async Task A_refusal_of_the_service_keeps_its_status_and_body()
    {
        var response = await _client.SendAsync(Signed(HttpMethod.Get, "/prd/api/v1/products/import/refused"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("bad header");
    }

    [Fact]
    public async Task Lines_come_back_while_the_request_body_is_still_being_sent()
    {
        var pipe = new System.IO.Pipelines.Pipe();
        var request = Signed(HttpMethod.Post, "/prd/api/v1/products/import");
        request.Content = new StreamContent(pipe.Reader.AsStream());
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

        var responding = _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("first\n"));
        await pipe.Writer.FlushAsync();

        var response = await responding.WaitAsync(TimeSpan.FromSeconds(5));   // headers arrive although the request is not complete
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
        (await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5))).Should().Be("echo:first");

        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("second\n"));
        await pipe.Writer.FlushAsync();
        (await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5))).Should().Be("echo:second");

        await pipe.Writer.CompleteAsync();
        (await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5))).Should().BeNull();
    }

    [Fact]
    public async Task An_sse_event_reaches_the_client_before_the_service_has_finished()
    {
        var started = DateTime.UtcNow;
        var response = await _client.SendAsync(Signed(HttpMethod.Get, "/prd/api/v1/products/import/abc/events"), HttpCompletionOption.ResponseHeadersRead);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());

        (await reader.ReadLineAsync()).Should().Be("event: progress");
        (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromMilliseconds(350), "the first event is not held back until the service ends (it waits 400 ms for the second)");
        await reader.ReadLineAsync();   // data
        await reader.ReadLineAsync();   // blank
        (await reader.ReadLineAsync()).Should().Be("event: completed");
    }

    [Fact]
    public async Task An_unreachable_service_is_a_502()
    {
        _serviceDown = true;

        var response = await _client.SendAsync(Signed(HttpMethod.Get, "/prd/api/v1/products/import/abc/events"));

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }
}

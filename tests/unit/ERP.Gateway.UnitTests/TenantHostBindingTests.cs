using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ERP.Shared.Authorization;
using Ocelot.API.Authorization;
using Xunit;

namespace ERP.Gateway.UnitTests;

/// <summary>
/// The Gateway is the single place where "tenant = request host" is enforced:
/// unknown hosts are rejected, client-sent tenant/user headers are discarded, and a
/// token only works on its own tenant's host.
/// </summary>
public class TenantHostBindingTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private sealed class FakeIdentity : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var host = Uri.UnescapeDataString(request.RequestUri!.Query.Split("host=")[1]);
            object? body = host switch
            {
                "a.erp.com" or "shop.a.com" => new ResolvedTenant(TenantA, "A", false),
                "b.erp.com" => new ResolvedTenant(TenantB, "B", false),
                _ => null
            };
            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body)) });
        }
    }

    private readonly FakeIdentity _identity = new();

    private TenantHostResolver Resolver()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(TenantHostResolver.HttpClientName))
            .Returns(() => new HttpClient(_identity, disposeHandler: false) { BaseAddress = new Uri("http://identity/") });
        return new TenantHostResolver(factory.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<TenantHostResolver>.Instance);
    }

    private static DefaultHttpContext Request(string host, string path = "/api/v1/sales/orders", Guid? tokenTenant = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (tokenTenant is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("tenant_id", tokenTenant.Value.ToString()),
            }, authenticationType: "Bearer"));
        }
        return context;
    }

    private static async Task<(bool NextCalled, HttpContext Context)> Run(HttpContext context, TenantHostResolver resolver)
    {
        var nextCalled = false;
        var middleware = new GatewayAuthorizationMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<GatewayAuthorizationMiddleware>.Instance,
            new ConfigurationBuilder().Build());
        await middleware.InvokeAsync(context, resolver);
        return (nextCalled, context);
    }

    [Fact]
    public async Task UnknownHost_IsRejectedWith404()
    {
        var (next, ctx) = await Run(Request("evil.com"), Resolver());

        next.Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ClientSuppliedTenantAndUserHeaders_AreReplacedByTheHostsTenant()
    {
        var request = Request("a.erp.com", "/api/v1/auth/login");
        request.Request.Headers["X-Tenant-Code"] = "B";
        request.Request.Headers["X-Tenant-Id"] = TenantB.ToString();
        request.Request.Headers["X-User-Id"] = "forged";
        request.Request.Headers["X-User-Permissions"] = "settings:system:manage";

        var (next, ctx) = await Run(request, Resolver());

        next.Should().BeTrue();
        ctx.Request.Headers["X-Tenant-Code"].ToString().Should().Be("A");
        ctx.Request.Headers["X-Tenant-Id"].ToString().Should().Be(TenantA.ToString());
        ctx.Request.Headers.ContainsKey("X-User-Id").Should().BeFalse();
        ctx.Request.Headers.ContainsKey("X-User-Permissions").Should().BeFalse();
    }

    [Fact]
    public async Task TokenOfTenantA_OnTenantBsHost_IsRejectedWith401()
    {
        var (next, ctx) = await Run(Request("b.erp.com", tokenTenant: TenantA), Resolver());

        next.Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task TokenOnItsOwnTenantsCustomDomain_PassesThrough()
    {
        var (next, ctx) = await Run(Request("shop.a.com", tokenTenant: TenantA), Resolver());

        next.Should().BeTrue();
        ctx.Request.Headers["X-Tenant-Id"].ToString().Should().Be(TenantA.ToString());
    }

    [Fact]
    public async Task HealthProbe_SkipsTenantResolution()
    {
        var (next, _) = await Run(Request("10.1.2.3", "/health"), Resolver());

        next.Should().BeTrue();
        _identity.Calls.Should().Be(0);
    }

    [Fact]
    public async Task HostResolution_IsCached()
    {
        var resolver = Resolver();

        await Run(Request("a.erp.com"), resolver);
        await Run(Request("a.erp.com"), resolver);

        _identity.Calls.Should().Be(1);
    }
}

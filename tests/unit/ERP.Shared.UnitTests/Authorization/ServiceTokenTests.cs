using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using ERP.Shared.Authorization;
using ERP.Shared.Authorization.ServiceAuth;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Security.Certificates;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ERP.Shared.UnitTests.Authorization;

/// <summary>
/// Service-to-service tokens (2026-10-21): each calling service signs its own short-lived token
/// with its own private key; the receiver accepts only the services it trusts, each with its
/// own published keys, and only on internal endpoints.
/// </summary>
public class ServiceTokenTests : IAsyncLifetime
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static readonly CertificateSet SalesOrderKeys = new EphemeralCertificateSource().Load();
    private static readonly CertificateSet ChatBotKeys = new EphemeralCertificateSource().Load();

    private readonly ServiceTokenIssuer _salesOrder = new("salesorder", SalesOrderKeys);
    private readonly ServiceTokenIssuer _chatBot = new("chatbot", ChatBotKeys);
    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var jwks = new Dictionary<string, string>
        {
            ["http://salesorder/internal/jwks.json"] = _salesOrder.JwksJson,
            ["http://chatbot/internal/jwks.json"] = _chatBot.JwksJson,
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ServiceAuth:Name"] = "warehouse",
            ["ServiceAuth:TrustedCallers:salesorder"] = "http://salesorder/internal/jwks.json",
            ["ServiceAuth:TrustedCallers:chatbot"] = "http://chatbot/internal/jwks.json",
            ["Jwt:Issuer"] = "ERP.Identity",
            ["Jwt:Audience"] = "ERP.Services",
        }).Build();

        _host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddErpJwtAuthentication(configuration);
                    services.AddErpServiceAuthentication(configuration);
                    services.PostConfigure<JwtBearerOptions>(ServiceAuthDefaults.Scheme, o =>
                        o.ConfigurationManager = new TrustedCallerKeys(
                            new Dictionary<string, string>
                            {
                                ["salesorder"] = "http://salesorder/internal/jwks.json",
                                ["chatbot"] = "http://chatbot/internal/jwks.json",
                            },
                            new StaticDocuments(jwks)));
                    services.AddErpAuthorization();
                    services.AddRequestTenantProvider();
                    services.AddControllers().AddApplicationPart(typeof(InternalProbeController).Assembly);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(e => e.MapControllers());
                }))
            .StartAsync();
        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    private async Task<HttpResponseMessage> CallAsync(string path, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task TrustedCaller_WithItsOwnKey_IsAccepted_AndItsTenantBecomesTheRequestTenant()
    {
        var response = await CallAsync("internal/v1/probe", _salesOrder.CreateToken("warehouse", Tenant, "user-1"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain(Tenant.ToString());
    }

    [Fact]
    public async Task TrustedService_NotListedOnTheEndpoint_IsForbidden()
    {
        (await CallAsync("internal/v1/probe", _chatBot.CreateToken("warehouse", Tenant, null)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CallerKey_CannotSignForAnotherCaller()
    {
        // chatbot's private key, but claiming to be salesorder.
        var impostor = new ServiceTokenIssuer("salesorder", ChatBotKeys);

        (await CallAsync("internal/v1/probe", impostor.CreateToken("warehouse", Tenant, null)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UntrustedService_IsRejected()
    {
        var unknown = new ServiceTokenIssuer("evil", new EphemeralCertificateSource().Load());

        (await CallAsync("internal/v1/probe", unknown.CreateToken("warehouse", Tenant, null)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenForAnotherService_IsRejected()
    {
        (await CallAsync("internal/v1/probe", _salesOrder.CreateToken("promotion", Tenant, null)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        var past = new ServiceTokenIssuer("salesorder", SalesOrderKeys, new FixedTime(DateTimeOffset.UtcNow.AddMinutes(-3)));

        (await CallAsync("internal/v1/probe", past.CreateToken("warehouse", Tenant, null)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NoToken_OrAUserToken_IsRejectedOnInternalEndpoints()
    {
        (await CallAsync("internal/v1/probe", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var userKey = new RsaSecurityKey(RSA.Create(2048));
        var userToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "ERP.Identity",
            Audience = "ERP.Services",
            Subject = new ClaimsIdentity(new[] { new Claim("sub", Guid.NewGuid().ToString()), new Claim("tenant_id", Tenant.ToString()) }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(userKey, SecurityAlgorithms.RsaSha256)
        });
        (await CallAsync("internal/v1/probe", userToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ServiceToken_IsRejectedOnUserEndpoints()
    {
        (await CallAsync("api/v1/probe", _salesOrder.CreateToken("warehouse", Tenant, null)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TenantRequired_ByDefault_TenantlessOnlyWhereDeclared()
    {
        var tenantless = _salesOrder.CreateToken("warehouse", tenantId: null, actorUserId: null);

        (await CallAsync("internal/v1/probe", tenantless)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CallAsync("internal/v1/probe/tenantless", tenantless)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Calling side ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handler_SendsTheCurrentTenant_AndRefusesToCallWithoutOne()
    {
        var recorder = new Recorder();
        var handler = new ServiceTokenHandler(_salesOrder, new HttpContextAccessor(), "warehouse", tenantless: false) { InnerHandler = recorder };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://warehouse/") };

        var withoutTenant = () => client.GetAsync("internal/v1/probe");
        await withoutTenant.Should().ThrowAsync<InvalidOperationException>();
        recorder.Requests.Should().BeEmpty();

        using (TenantScope.Begin(Tenant))
            await client.GetAsync("internal/v1/probe");

        var token = new JsonWebToken(recorder.Requests.Single().Headers.Authorization!.Parameter);
        token.Issuer.Should().Be("erp-svc:salesorder");
        token.Audiences.Should().Equal("erp-svc:warehouse");
        token.GetClaim("tenant_id").Value.Should().Be(Tenant.ToString());
        (token.ValidTo - token.IssuedAt).Should().BeCloseTo(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Issuer_ReusesATokenWhileItIsFresh_PerTargetTenantAndActor()
    {
        var first = _salesOrder.CreateToken("warehouse", Tenant, "u");

        _salesOrder.CreateToken("warehouse", Tenant, "u").Should().Be(first);
        _salesOrder.CreateToken("warehouse", Guid.NewGuid(), "u").Should().NotBe(first);
        _salesOrder.CreateToken("promotion", Tenant, "u").Should().NotBe(first);
    }

    [Fact]
    public void Jwks_PublishesOnlyPublicKeys_WithTheTokenKid()
    {
        var keys = new JsonWebKeySet(_salesOrder.JwksJson).Keys;
        var kid = new JsonWebToken(_salesOrder.CreateToken("warehouse", Tenant, null)).Kid;

        keys.Should().ContainSingle().Which.Kid.Should().Be(kid);
        keys.Single().D.Should().BeNullOrEmpty();
    }

    private sealed class StaticDocuments(IReadOnlyDictionary<string, string> documents) : IDocumentRetriever
    {
        public Task<string> GetDocumentAsync(string address, CancellationToken cancel) => Task.FromResult(documents[address]);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Recorder : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}

[ApiController]
public class InternalProbeController : ControllerBase
{
    [HttpGet("internal/v1/probe")]
    [AuthorizeInternalService("salesorder")]
    public IActionResult Probe([FromServices] ITenantProvider tenants) => Ok(new { tenant = tenants.CurrentTenantId });

    [HttpGet("internal/v1/probe/tenantless")]
    [AuthorizeInternalService("salesorder", RequiresTenant = false)]
    public IActionResult Tenantless() => Ok();

    [HttpGet("api/v1/probe")]
    [AuthorizeAnyAuthenticatedUser]
    public IActionResult UserEndpoint() => Ok();
}

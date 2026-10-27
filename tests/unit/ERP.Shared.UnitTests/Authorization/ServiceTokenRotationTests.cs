using System.Net;
using System.Net.Http.Headers;
using ERP.Shared.Authorization;
using ERP.Shared.Authorization.ServiceAuth;
using ERP.Shared.Security.Certificates;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Xunit;

namespace ERP.Shared.UnitTests.Authorization;

/// <summary>
/// R38: service tokens use an ephemeral key, so a caller that restarts signs with a NEW key. The receiver used to refuse the first call after that (the
/// key refresh ran in the background); now the keys are fetched again before the token is judged, throttled so unknown keys cannot flood the caller.
/// </summary>
public class ServiceTokenRotationTests : IAsyncLifetime
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private const string JwksUrl = "http://salesorder/internal/jwks.json";

    private ServiceTokenIssuer _salesOrder = new("salesorder", new EphemeralCertificateSource().Load());
    private readonly Documents _documents = new();
    private readonly MovableTime _time = new(DateTimeOffset.UtcNow);
    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _documents.Json = _salesOrder.JwksJson;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ServiceAuth:Name"] = "warehouse",
            ["ServiceAuth:TrustedCallers:salesorder"] = JwksUrl,
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
                        o.ConfigurationManager = new TrustedCallerKeys(new Dictionary<string, string> { ["salesorder"] = JwksUrl }, _documents, null, _time));
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

    private async Task<HttpStatusCode> CallAsync(ServiceTokenIssuer issuer)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "internal/v1/probe");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", issuer.CreateToken("warehouse", Tenant, "user-1"));
        return (await _client.SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task A_caller_that_restarted_with_a_new_key_is_accepted_on_its_first_call()
    {
        (await CallAsync(_salesOrder)).Should().Be(HttpStatusCode.OK);

        // salesorder is redeployed: same name, new key, new JWKS. The receiver still has the old key cached.
        _salesOrder = new ServiceTokenIssuer("salesorder", new EphemeralCertificateSource().Load());
        _documents.Json = _salesOrder.JwksJson;
        _time.Advance(TimeSpan.FromSeconds(3));

        (await CallAsync(_salesOrder)).Should().Be(HttpStatusCode.OK, "the new key is fetched before the token is judged");
    }

    [Fact]
    public async Task A_token_signed_with_an_unknown_key_is_still_refused_and_fetches_are_throttled()
    {
        (await CallAsync(_salesOrder)).Should().Be(HttpStatusCode.OK);
        var fetchesBefore = _documents.Fetches;
        var attacker = new ServiceTokenIssuer("salesorder", new EphemeralCertificateSource().Load());   // claims to be salesorder, key not published

        _time.Advance(TimeSpan.FromSeconds(3));
        for (var i = 0; i < 5; i++)
            (await CallAsync(attacker)).Should().Be(HttpStatusCode.Unauthorized);

        (_documents.Fetches - fetchesBefore).Should().Be(1, "five unknown-key calls inside the minimum interval cause one fetch, not five");

        _time.Advance(TimeSpan.FromSeconds(3));
        (await CallAsync(attacker)).Should().Be(HttpStatusCode.Unauthorized);
        (_documents.Fetches - fetchesBefore).Should().Be(2, "another fetch is allowed once the interval has passed");
        (await CallAsync(_salesOrder)).Should().Be(HttpStatusCode.OK, "the real caller is not affected");
    }

    [Fact]
    public async Task A_caller_that_is_down_while_it_restarts_is_picked_up_after_it_is_back()
    {
        (await CallAsync(_salesOrder)).Should().Be(HttpStatusCode.OK);
        _salesOrder = new ServiceTokenIssuer("salesorder", new EphemeralCertificateSource().Load());

        _documents.Down = true;           // its JWKS endpoint is not up yet
        _time.Advance(TimeSpan.FromSeconds(3));
        (await CallAsync(_salesOrder)).Should().Be(HttpStatusCode.Unauthorized);

        _documents.Down = false;
        _documents.Json = _salesOrder.JwksJson;
        _time.Advance(TimeSpan.FromSeconds(3));
        (await CallAsync(_salesOrder)).Should().Be(HttpStatusCode.OK);
    }

    private sealed class Documents : IDocumentRetriever
    {
        public string Json { get; set; } = "{}";
        public bool Down { get; set; }
        public int Fetches { get; private set; }

        public Task<string> GetDocumentAsync(string address, CancellationToken cancel)
        {
            Fetches++;
            return Down ? throw new HttpRequestException("connection refused") : Task.FromResult(Json);
        }
    }

    private sealed class MovableTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}

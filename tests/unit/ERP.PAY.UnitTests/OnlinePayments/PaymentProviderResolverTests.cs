using System.Net;
using ERP.PAY.Application.DTOs.Settings;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Entities.Settings;
using ERP.PAY.Infrastructure.Gateway;
using ERP.PAY.Infrastructure.Persistence;
using ERP.PAY.Infrastructure.Services;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using static ERP.PAY.UnitTests.Gateways.GatewayTestSupport;

namespace ERP.PAY.UnitTests.OnlinePayments;

/// <summary>ROADMAP P1-10: which gateways a tenant can use is decided by the platform switch, the tenant's own switch and its keys.</summary>
public class PaymentProviderResolverTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    /// <summary>"Encrypts" by wrapping, so a test can see that a stored value is not the plaintext.</summary>
    private sealed class FakeProtector : ISecretProtector
    {
        public string Protect(string plaintext) => "enc:" + plaintext;
        public string Unprotect(string ciphertext) => ciphertext.StartsWith("enc:") ? ciphertext[4..] : throw new InvalidOperationException("not encrypted");
    }

    private readonly PaymentDbContext _db;
    private readonly FakeProtector _protector = new();

    public PaymentProviderResolverTests()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new PaymentDbContext(options, new TenantProvider(() => null));
    }

    private PaymentProviderResolver Resolver(IConfiguration? config = null)
    {
        var http = Http((_, _) => (HttpStatusCode.OK, "{}")).Factory;
        var providers = new IPaymentProvider[]
        {
            new VnPayGateway(config ?? Config(), Log<VnPayGateway>(), http), new MoMoGateway(config ?? Config(), Log<MoMoGateway>(), http),
            new ZaloPayGateway(config ?? Config(), Log<ZaloPayGateway>(), http), new NullPaymentProvider(Log<NullPaymentProvider>())
        };
        return new PaymentProviderResolver(_db, _protector, config ?? Config(), providers);
    }

    private async Task Configure(Guid tenant, string provider, bool enabled, string? publicId = "PUB", string? access = "ACC", string? s1 = "S1", string? s2 = "S2")
    {
        var row = TenantGatewayCredential.Create(tenant, provider);
        row.Update(enabled, true, publicId, access == null ? null : _protector.Protect(access), s1 == null ? null : _protector.Protect(s1), s2 == null ? null : _protector.Protect(s2));
        _db.TenantGatewayCredentials.Add(row);
        using (TenantScope.Begin(tenant)) await _db.SaveChangesAsync();   // a save must know its tenant
    }

    [Fact]
    public async Task A_gateway_with_the_tenants_keys_resolves_with_decrypted_credentials()
    {
        await Configure(TenantA, "MoMo", enabled: true);
        using var _ = TenantScope.Begin(TenantA);

        var resolved = await Resolver().ResolveAsync("momo");

        resolved.Provider.Code.Should().Be("MoMo");
        resolved.Credentials.Should().BeEquivalentTo(new GatewayCredentials("MoMo", "PUB", "ACC", "S1", "S2", true));
    }

    [Fact]
    public async Task Another_tenants_keys_are_never_used()
    {
        await Configure(TenantA, "MoMo", enabled: true);
        using var _ = TenantScope.Begin(TenantB);

        await FluentActions.Awaiting(() => Resolver().ResolveAsync("MoMo")).Should().ThrowAsync<BusinessRuleException>();
    }

    [Theory]
    [InlineData(false, "PUB", "ACC", "S1", "S2")]   // switched off by the tenant
    [InlineData(true, "PUB", null, "S1", "S2")]     // MoMo without an access key
    [InlineData(true, "PUB", "ACC", null, "S2")]    // no secret
    [InlineData(true, null, "ACC", "S1", "S2")]     // no partner code
    public async Task A_gateway_that_is_off_or_incomplete_is_refused(bool enabled, string? pub, string? access, string? s1, string? s2)
    {
        await Configure(TenantA, "MoMo", enabled, pub, access, s1, s2);
        using var _ = TenantScope.Begin(TenantA);

        await FluentActions.Awaiting(() => Resolver().ResolveAsync("MoMo")).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task The_platform_switch_overrides_the_tenants()
    {
        await Configure(TenantA, "ZaloPay", enabled: true);
        var system = SystemPaymentSettings.CreateDefault();
        system.Update(true, true, moMoAvailable: true, zaloPayAvailable: false);
        _db.SystemPaymentSettings.Add(system);
        await _db.SaveChangesAsync();
        using var _ = TenantScope.Begin(TenantA);

        await FluentActions.Awaiting(() => Resolver().ResolveAsync("ZaloPay")).Should().ThrowAsync<BusinessRuleException>().WithMessage("*not available*");
    }

    [Fact]
    public async Task ZaloPay_needs_key2_to_verify_callbacks()
    {
        await Configure(TenantA, "ZaloPay", enabled: true, s2: null);
        using var _ = TenantScope.Begin(TenantA);

        await FluentActions.Awaiting(() => Resolver().ResolveAsync("ZaloPay")).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task The_methods_a_shop_offers_are_cod_plus_the_gateways_it_can_really_use()
    {
        await Configure(TenantA, "MoMo", enabled: true);
        await Configure(TenantA, "ZaloPay", enabled: false);
        using var _ = TenantScope.Begin(TenantA);

        var methods = await Resolver().ListMethodsAsync();

        methods.Select(m => m.Code).Should().Equal("COD", "MoMo");
        methods.Single(m => m.Code == "COD").IsGateway.Should().BeFalse();
    }

    [Fact]
    public async Task Cod_can_be_switched_off_per_tenant()
    {
        var settings = TenantPaymentSettings.CreateDefault(TenantA);
        settings.Update(false, null, true, codEnabled: false);
        _db.TenantPaymentSettings.Add(settings);
        using var _ = TenantScope.Begin(TenantA);
        await _db.SaveChangesAsync();

        (await Resolver().ListMethodsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task VNPay_still_works_from_the_global_configuration_as_before()
    {
        using var _ = TenantScope.Begin(TenantA);
        var config = Config(("VnPay:TmnCode", "TMN"), ("VnPay:HashSecret", "SECRET"));

        var resolved = await Resolver(config).ResolveAsync("VNPay");

        resolved.Credentials.Should().BeEquivalentTo(new GatewayCredentials("VNPay", "TMN", null, "SECRET", null, true));
        (await Resolver().ListMethodsAsync()).Select(m => m.Code).Should().Equal("COD");   // no config, no tenant keys: not offered
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("Null", true)]
    [InlineData("COD", true)]
    [InlineData("MoMo", false)]
    public void A_payment_that_never_went_through_a_gateway_is_refunded_by_the_stub(string? gatewayProvider, bool stub)
    {
        (Resolver().StubFor(gatewayProvider) != null).Should().Be(stub);
    }

    [Fact]
    public async Task Saved_secrets_are_encrypted_and_never_returned_by_the_settings_service()
    {
        using var _ = TenantScope.Begin(TenantA);
        var service = new PaymentSettingsService(_db, _protector, Mock.Of<ERP.Shared.Infrastructure.Caching.ICacheService>());

        var dto = await service.UpdateGatewayAsync(TenantA, "momo", new UpdateGatewayCredentialRequest(true, true, "PARTNER", "ACCESS", "SECRET", null), default);

        dto.Should().BeEquivalentTo(new GatewayCredentialDto("MoMo", true, true, "PARTNER", true, true, false));
        var row = await _db.TenantGatewayCredentials.IgnoreQueryFilters().SingleAsync();
        row.Secret1Encrypted.Should().Be("enc:SECRET");
        row.AccessKey.Should().Be("enc:ACCESS");

        // An empty secret on the next save keeps the stored one.
        await service.UpdateGatewayAsync(TenantA, "MoMo", new UpdateGatewayCredentialRequest(true, false, "PARTNER", "", "", null), default);
        (await _db.TenantGatewayCredentials.IgnoreQueryFilters().SingleAsync()).Secret1Encrypted.Should().Be("enc:SECRET");

        (await service.GetGatewaysAsync(TenantA, default)).Select(g => g.Provider).Should().Equal("VNPay", "MoMo", "ZaloPay");
        await FluentActions.Awaiting(() => service.UpdateGatewayAsync(TenantA, "COD", new UpdateGatewayCredentialRequest(true, true, null, null, null, null), default))
            .Should().ThrowAsync<InvalidOperationException>();
    }
}

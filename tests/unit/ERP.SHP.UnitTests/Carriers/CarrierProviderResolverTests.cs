using System.Net;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Security;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities.Settings;
using ERP.SHP.Infrastructure.Carriers;
using ERP.SHP.Infrastructure.Persistence;
using ERP.SHP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ERP.SHP.UnitTests.Carriers.CarrierTestSupport;

namespace ERP.SHP.UnitTests.Carriers;

/// <summary>ROADMAP P1-11: which carriers a shop can use - platform switch, shop switch, complete account.</summary>
public class CarrierProviderResolverTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed class FakeProtector : ISecretProtector
    {
        public string Protect(string plaintext) => "enc:" + plaintext;
        public string Unprotect(string ciphertext) => ciphertext.StartsWith("enc:") ? ciphertext[4..] : throw new InvalidOperationException("not encrypted");
    }

    private readonly ShippingDbContext _db;
    private readonly FakeProtector _protector = new();

    public CarrierProviderResolverTests()
    {
        var options = new DbContextOptionsBuilder<ShippingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new ShippingDbContext(options, new TenantProvider(() => Tenant.ToString()));
    }

    private CarrierProviderResolver Resolver()
    {
        var http = Http((_, _) => (HttpStatusCode.OK, "{}")).Factory;
        var providers = new ICarrierProvider[]
        {
            new GhtkCarrierProvider(Config(), Log<GhtkCarrierProvider>(), http), new GhnCarrierProvider(Config(), Log<GhnCarrierProvider>(), http),
            new ViettelPostCarrierProvider(Config(), Log<ViettelPostCarrierProvider>(), http), new JAndTCarrierProvider()
        };
        return new CarrierProviderResolver(_db, _protector, providers);
    }

    private void Account(string code, bool enabled, string? publicId, string? username, string? secret, string? webhook = null)
    {
        var row = TenantCarrierCredential.Create(Tenant, code);
        row.Update(enabled, true, publicId, username, secret == null ? null : _protector.Protect(secret), webhook == null ? null : _protector.Protect(webhook));
        _db.TenantCarrierCredentials.Add(row);
        _db.SaveChanges();
    }

    [Fact]
    public async Task A_shop_without_accounts_has_no_carrier_and_a_clear_reason()
    {
        (await Resolver().ListEnabledAsync()).Should().BeEmpty();

        var act = () => Resolver().ResolveAsync("GHTK");
        (await act.Should().ThrowAsync<BusinessRuleException>()).WithMessage("*not enabled*");
    }

    [Fact]
    public async Task An_enabled_complete_account_is_decrypted_and_handed_to_the_provider()
    {
        Account("GHTK", true, "PARTNER1", null, "TOKEN1", "HOOK");

        var resolved = await Resolver().ResolveAsync("ghtk");

        resolved.Provider.Code.Should().Be("GHTK");
        resolved.Credentials.Should().BeEquivalentTo(new CarrierCredentials("GHTK", "PARTNER1", null, "TOKEN1", "HOOK", true));
    }

    [Fact]
    public async Task Incomplete_disabled_or_platform_switched_off_accounts_are_not_usable()
    {
        Account("GHTK", true, null, null, "TOKEN1");          // no partner code
        Account("GHN", false, "123", null, "TOKEN2");          // switched off by the shop
        Account("VTP", true, null, "user@x.test", "pw");       // complete
        var system = SystemShippingSettings.CreateDefault();
        system.UpdateCarriers(ghtk: true, vtp: false, ghn: true, jnt: true);
        _db.SystemShippingSettings.Add(system);
        _db.SaveChanges();

        (await Resolver().ListEnabledAsync()).Should().BeEmpty("VTP is off on the platform, GHN is off for the shop, GHTK has no partner code");
        var act = () => Resolver().ResolveAsync("VTP");
        (await act.Should().ThrowAsync<BusinessRuleException>()).WithMessage("*not available on this platform*");
    }

    [Fact]
    public async Task The_jandt_placeholder_and_unknown_carriers_cannot_be_resolved_even_with_an_account()
    {
        Account("JNT", true, "X", null, "KEY");

        var jnt = () => Resolver().ResolveAsync("JNT");
        (await jnt.Should().ThrowAsync<BusinessRuleException>()).WithMessage("*not integrated*");
        var unknown = () => Resolver().ResolveAsync("FEDEX");
        await unknown.Should().ThrowAsync<BusinessRuleException>();
        (await Resolver().ListEnabledAsync()).Should().BeEmpty();
    }

    [Fact]
    public void A_blank_secret_on_update_keeps_the_stored_one()
    {
        var row = TenantCarrierCredential.Create(Tenant, "GHTK");
        row.Update(true, true, "P", null, "enc:one", null);

        row.Update(true, false, "P2", null, "", null);

        row.Secret1Encrypted.Should().Be("enc:one");
        row.PublicId.Should().Be("P2");
        row.Sandbox.Should().BeFalse();
    }
}

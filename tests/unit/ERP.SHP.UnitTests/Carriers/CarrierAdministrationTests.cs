using System.Net;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Security;
using ERP.SHP.Application.DTOs;
using ERP.SHP.Application.DTOs.Settings;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Enums;
using ERP.SHP.Infrastructure.Carriers;
using ERP.SHP.Infrastructure.Persistence;
using ERP.SHP.Infrastructure.Repositories;
using ERP.SHP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using static ERP.SHP.UnitTests.Carriers.CarrierTestSupport;

namespace ERP.SHP.UnitTests.Carriers;

/// <summary>ROADMAP P1-11: the shop's carrier accounts, selection rules and the shipments list, against a real (in-memory) DbContext.</summary>
public class CarrierAdministrationTests
{
    private static readonly Guid Tenant = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class FakeProtector : ISecretProtector
    {
        public string Protect(string plaintext) => "enc:" + plaintext;
        public string Unprotect(string ciphertext) => ciphertext[4..];
    }

    private readonly ShippingDbContext _db;

    public CarrierAdministrationTests()
    {
        var options = new DbContextOptionsBuilder<ShippingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new ShippingDbContext(options, new TenantProvider(() => Tenant.ToString()));
    }

    private ICarrierProviderResolver Resolver() =>
        new CarrierProviderResolver(_db, new FakeProtector(), new ICarrierProvider[]
        {
            new GhtkCarrierProvider(Config(), Log<GhtkCarrierProvider>(), Http((_, _) => (HttpStatusCode.OK, "{}")).Factory),
            new ViettelPostCarrierProvider(Config(), Log<ViettelPostCarrierProvider>(), Http((_, _) => (HttpStatusCode.OK, "{}")).Factory),
            new JAndTCarrierProvider()
        });

    private ShippingSettingsService Settings()
    {
        var cache = new Mock<ICacheService>();
        return new ShippingSettingsService(_db, cache.Object, new FakeProtector(), Resolver());
    }

    // ── accounts ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Saving_an_account_stores_the_secrets_encrypted_and_never_returns_them()
    {
        var saved = await Settings().UpdateCarrierAccountAsync(Tenant, "ghtk", new UpdateCarrierAccountRequest(true, true, "PARTNER1", null, "API-TOKEN", "HOOK"), default);

        saved.Should().BeEquivalentTo(new CarrierAccountDto("GHTK", "Giao Hàng Tiết Kiệm (GHTK)", true, true, true, true, "PARTNER1", null, true, true, true));
        var row = await _db.TenantCarrierCredentials.AsNoTracking().SingleAsync();
        row.Secret1Encrypted.Should().Be("enc:API-TOKEN").And.NotContain("API-TOKEN" + "x");
        row.Secret2Encrypted.Should().Be("enc:HOOK");

        var listed = await Settings().GetCarrierAccountsAsync(Tenant, default);
        listed.Select(a => a.Code).Should().BeEquivalentTo("GHTK", "VTP", "GHN", "JNT");
        listed.Single(a => a.Code == "JNT").Integrated.Should().BeFalse();
        listed.Single(a => a.Code == "VTP").Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task A_blank_secret_keeps_the_stored_one_and_an_incomplete_account_cannot_be_switched_on()
    {
        await Settings().UpdateCarrierAccountAsync(Tenant, "GHTK", new UpdateCarrierAccountRequest(true, true, "P", null, "TOKEN", null), default);

        var again = await Settings().UpdateCarrierAccountAsync(Tenant, "GHTK", new UpdateCarrierAccountRequest(true, false, "P2", null, "", null), default);

        again.Sandbox.Should().BeFalse();
        again.PublicId.Should().Be("P2");
        (await _db.TenantCarrierCredentials.AsNoTracking().SingleAsync()).Secret1Encrypted.Should().Be("enc:TOKEN");

        var act = () => Settings().UpdateCarrierAccountAsync(Tenant, "VTP", new UpdateCarrierAccountRequest(true, true, null, "user@x.test", null, null), default);
        (await act.Should().ThrowAsync<BusinessRuleException>()).WithMessage("*cannot be enabled*login name and password*");
    }

    [Fact]
    public async Task An_unknown_or_not_yet_integrated_carrier_cannot_be_set_up()
    {
        var unknown = () => Settings().UpdateCarrierAccountAsync(Tenant, "FEDEX", new UpdateCarrierAccountRequest(true, true, "x", null, "y", null), default);
        await unknown.Should().ThrowAsync<BusinessRuleException>();

        var jnt = () => Settings().UpdateCarrierAccountAsync(Tenant, "JNT", new UpdateCarrierAccountRequest(true, true, "x", null, "y", null), default);
        (await jnt.Should().ThrowAsync<BusinessRuleException>()).WithMessage("*not integrated*");
    }

    [Fact]
    public async Task The_pickup_address_and_default_weight_are_saved_and_a_nonsense_weight_is_refused()
    {
        var saved = await Settings().UpdateTenantSettingsAsync(Tenant,
            new UpdateTenantShippingSettingsRequest(0, 0, "Shop", "0900000001", "1 Nguyen Hue", "Ben Nghe", "Quan 1", "Ho Chi Minh", 350), default);

        (saved.SenderName, saved.SenderProvince, saved.DefaultItemWeightGrams).Should().Be(("Shop", "Ho Chi Minh", 350));
        (await _db.TenantShippingSettings.AsNoTracking().SingleAsync()).HasSender.Should().BeTrue();

        var act = () => Settings().UpdateTenantSettingsAsync(Tenant, new UpdateTenantShippingSettingsRequest(0, 0, DefaultItemWeightGrams: 0), default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task The_platform_can_switch_a_carrier_off_for_every_shop()
    {
        await Settings().UpdateCarrierAccountAsync(Tenant, "GHTK", new UpdateCarrierAccountRequest(true, true, "P", null, "T", null), default);

        await Settings().UpdateSystemSettingsAsync(new UpdateSystemShippingSettingsRequest(true, GhtkAvailable: false), default);

        (await Settings().GetCarrierAccountsAsync(Tenant, default)).Single(a => a.Code == "GHTK").PlatformAvailable.Should().BeFalse();
        (await Resolver().ListEnabledAsync()).Should().BeEmpty();
    }

    // ── rules ────────────────────────────────────────────────────────────

    private CarrierRuleService Rules() => new(_db, Resolver());

    [Fact]
    public async Task Rules_are_created_updated_ordered_and_deleted()
    {
        var slow = await Rules().CreateAsync(new SaveCarrierRuleRequest("Rest", 50, true, null, null, null, null, null, null, "ghtk"), default);
        var first = await Rules().CreateAsync(new SaveCarrierRuleRequest("Big COD", 10, true, "Hà Nội", 2000, null, null, null, true, "VTP"), default);

        (await Rules().ListAsync(default)).Select(r => r.Name).Should().Equal("Big COD", "Rest");
        first.CarrierCode.Should().Be("VTP");

        var changed = await Rules().UpdateAsync(slow.Id, new SaveCarrierRuleRequest("Rest", 5, false, null, null, null, null, null, null, "GHTK"), default);
        changed.Should().Match<CarrierRuleDto>(r => r.Priority == 5 && !r.IsActive);

        await Rules().DeleteAsync(first.Id, default);
        (await Rules().ListAsync(default)).Should().ContainSingle();
    }

    [Fact]
    public async Task A_rule_with_an_unknown_carrier_contradictory_limits_or_the_unintegrated_carrier_is_a_business_error()
    {
        foreach (var bad in new[]
        {
            new SaveCarrierRuleRequest("x", 1, true, null, null, null, null, null, null, "FEDEX"),
            new SaveCarrierRuleRequest("x", 1, true, null, 900, 100, null, null, null, "GHTK"),
            new SaveCarrierRuleRequest("x", 1, true, null, null, null, null, null, null, "JNT"),
            new SaveCarrierRuleRequest(" ", 1, true, null, null, null, null, null, null, "GHTK"),
        })
        {
            var act = () => Rules().CreateAsync(bad, default);
            await act.Should().ThrowAsync<BusinessRuleException>();
        }
    }

    // ── shipments list ───────────────────────────────────────────────────

    private Shipment Add(string name, string tracking, bool review = false, ShipmentStatus? deliver = null)
    {
        var s = Shipment.Create(Guid.NewGuid(), Guid.NewGuid(), "GHTK", new ShipmentRecipient(name, "0911", "s", null, "d", "p", null),
            new List<(Guid, string, int, decimal)> { (Guid.NewGuid(), "i", 1, 1m) });
        s.AssignTrackingNumber(tracking);
        if (review) s.FlagForReview("check the cash");
        if (deliver == ShipmentStatus.Delivered) s.ApplyCarrierStatus(CarrierShipmentStatus.Delivered, "x", "ok");
        _db.Shipments.Add(s);
        _db.SaveChanges();
        return s;
    }

    [Fact]
    public async Task The_shipments_list_filters_by_status_search_and_needs_review()
    {
        var a = Add("An Nguyen", "TRK-A");
        var b = Add("Binh Tran", "TRK-B", review: true);
        var c = Add("Chi Le", "TRK-C", deliver: ShipmentStatus.Delivered);
        var repo = new ShipmentRepository(_db);

        (await repo.ListAsync(0, 20, null, null, false)).Total.Should().Be(3);
        (await repo.ListAsync(0, 20, ShipmentStatus.Delivered, null, false)).Items.Single().Id.Should().Be(c.Id);
        (await repo.ListAsync(0, 20, null, "TRK-A", false)).Items.Single().Id.Should().Be(a.Id);
        (await repo.ListAsync(0, 20, null, "Binh", false)).Items.Single().Id.Should().Be(b.Id);
        (await repo.ListAsync(0, 20, null, null, true)).Items.Single().Id.Should().Be(b.Id);
        (await repo.ListAsync(2, 1, null, null, false)).Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Tracking_lookup_finds_the_tenant_across_tenants_and_the_stale_query_ignores_final_and_unbooked_shipments()
    {
        var open = Add("An", "TRK-OPEN");
        Add("Done", "TRK-DONE", deliver: ShipmentStatus.Delivered);
        var repo = new ShipmentRepository(_db);

        (await repo.FindTenantByTrackingNumberAsync("TRK-OPEN")).Should().Be(Tenant);
        (await repo.FindTenantByTrackingNumberAsync("NOPE")).Should().BeNull();
        (await repo.GetStaleInCarrierHandsAsync(DateTime.UtcNow.AddMinutes(1), 10)).Select(s => s.Id).Should().Equal(open.Id);
        (await repo.GetStaleInCarrierHandsAsync(DateTime.UtcNow.AddDays(-1), 10)).Should().BeEmpty();
        (await repo.GetTenantsWithOpenShipmentsAsync()).Should().Equal(Tenant);
    }

    [Fact]
    public async Task Webhook_log_entries_are_unique_per_carrier_parcel_status_and_time()
    {
        var repo = new ShipmentRepository(_db);
        var when = new DateTime(2026, 10, 29, 3, 0, 0, DateTimeKind.Utc);
        await repo.AddWebhookLogAsync(CarrierWebhookLog.Create(Tenant, "GHTK", "TRK", "5", when));
        await _db.SaveChangesAsync();

        (await repo.WebhookSeenAsync("GHTK", "TRK", "5", when)).Should().BeTrue();
        (await repo.WebhookSeenAsync("GHTK", "TRK", "4", when)).Should().BeFalse();
        (await repo.WebhookSeenAsync("GHN", "TRK", "5", when)).Should().BeFalse();
    }
}

using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.ItemProfiles;
using ERP.MFG.Application.Features.Settings;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Infrastructure.Services;
using ERP.MFG.UnitTests.Common;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace ERP.MFG.UnitTests.Plants;

public class SettingsTests
{
    private static ProductionSettingValues Defaults => ProductionSettingValues.Defaults;

    // ── Domain ranges (TC-SET-002) ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, true)]
    [InlineData(100, true)]
    [InlineData(100.01, false)]
    [InlineData(-1, false)]
    public void TC_SET_002_Tolerance_Boundaries(double value, bool valid)
    {
        var setting = ProductionSetting.CreateDefault(Guid.NewGuid());
        var act = () => setting.Update(Defaults with { OverProductionTolerancePct = (decimal)value }, false);

        if (valid) act.Should().NotThrow(); else act.Should().Throw<BusinessRuleException>();
    }

    [Theory]
    [InlineData(6)]
    [InlineData(366)]
    public void Mrp_Horizon_Outside_7_To_365_Is_Refused_With_The_Spec_Code(int days)
    {
        var setting = ProductionSetting.CreateDefault(Guid.NewGuid());

        var act = () => setting.Update(Defaults with { MrpHorizonDays = days }, false);

        act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("MFG_MRP_002");
    }

    [Fact]
    public void Defaults_Are_The_Spec_Defaults()
    {
        var d = ProductionSetting.CreateDefault(Guid.NewGuid()).ToValues();

        d.Should().Be(new ProductionSettingValues(0, 0, 0, false, false, true, true, IssueMethod.Manual, CostingMethod.Actual, 90, 20, "{ItemCode}-{yyMMdd}-{seq2}"));
    }

    // ── BR-SET-02: audit of old and new ──────────────────────────────────────────────────────────────────

    [Fact]
    public void TC_SET_004_Update_Returns_Exactly_The_Changed_Fields_With_Old_And_New()
    {
        var setting = ProductionSetting.CreateDefault(Guid.NewGuid());

        var changes = setting.Update(Defaults with { OverProductionTolerancePct = 7, BlockReleaseOnShortage = true }, false);

        changes.Should().BeEquivalentTo(new[]
        {
            new SettingChange("OverProductionTolerancePct", "0", "7"),
            new SettingChange("BlockReleaseOnShortage", "False", "True"),
        });
        setting.OverProductionTolerancePct.Should().Be(7);
    }

    [Fact]
    public void The_Audit_Writes_Decimals_Without_Trailing_Zeros()
    {
        var setting = ProductionSetting.CreateDefault(Guid.NewGuid());
        setting.Update(Defaults with { OverIssueTolerancePct = 5.50m }, false);

        var changes = setting.Update(Defaults with { OverIssueTolerancePct = 7.00m }, false);

        changes.Should().ContainSingle().Which.Should().Be(new SettingChange("OverIssueTolerancePct", "5.5", "7"));
    }

    [Fact]
    public void Saving_Unchanged_Values_Is_A_No_Op()
    {
        var setting = ProductionSetting.CreateDefault(Guid.NewGuid());

        setting.Update(Defaults, false).Should().BeEmpty();
    }

    // ── BR-SET-03: costing method ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TC_SET_005_Costing_Method_Cannot_Change_While_Orders_Are_Open()
    {
        var setting = ProductionSetting.CreateDefault(Guid.NewGuid());

        var act = () => setting.Update(Defaults with { CostingMethod = CostingMethod.Standard }, openOrdersExist: true);

        act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("MFG_SET_001");
        setting.CostingMethod.Should().Be(CostingMethod.Actual);
    }

    [Fact]
    public void TC_SET_006_Costing_Method_Can_Change_When_No_Order_Is_Open()
    {
        var setting = ProductionSetting.CreateDefault(Guid.NewGuid());

        setting.Update(Defaults with { CostingMethod = CostingMethod.Standard }, openOrdersExist: false);

        setting.CostingMethod.Should().Be(CostingMethod.Standard);
    }

    [Fact]
    public void Other_Fields_May_Change_Even_While_Orders_Are_Open()
    {
        var setting = ProductionSetting.CreateDefault(Guid.NewGuid());

        setting.Update(Defaults with { OverIssueTolerancePct = 3 }, openOrdersExist: true);

        setting.OverIssueTolerancePct.Should().Be(3);
    }

    // ── Handler: audit rows, cache, guard asked only when needed ─────────────────────────────────────────

    private static (UpdateSettingsHandler Handler, Mock<IProductionSettingProvider> Provider, Plant Plant) Arrange(Fakes f)
    {
        var plant = f.AddPlant();
        var provider = new Mock<IProductionSettingProvider>();
        var access = new PlantAccessService(f.PlantRepo, f.AccessRepo, new FakeUser(Guid.NewGuid(), PermissionConstants.Manufacturing_SettingsManage));
        var handler = new UpdateSettingsHandler(access, f.SettingRepo, f.Guard, provider.Object, new FakeUser(Guid.Parse("11111111-1111-1111-1111-111111111111")), f.Uow);
        return (handler, provider, plant);
    }

    [Fact]
    public async Task TC_SET_003_004_A_Change_Is_Audited_Saved_Once_And_The_Cache_Is_Invalidated()
    {
        var f = new Fakes();
        var (handler, provider, plant) = Arrange(f);

        var result = await handler.Handle(new UpdateSettingsCommand(plant.Id, Defaults with { OverProductionTolerancePct = 7 }), default);

        result.Values.OverProductionTolerancePct.Should().Be(7);
        f.Audits.Should().ContainSingle().Which.Should().Match<ProductionSettingAudit>(a =>
            a.Field == "OverProductionTolerancePct" && a.OldValue == "0" && a.NewValue == "7" && a.ChangedBy == "11111111-1111-1111-1111-111111111111");
        f.Saves.Should().Be(1);
        provider.Verify(p => p.Invalidate(plant.Id), Times.Once);
    }

    [Fact]
    public async Task An_Update_Without_Changes_Saves_And_Invalidates_Nothing()
    {
        var f = new Fakes();
        var (handler, provider, plant) = Arrange(f);

        await handler.Handle(new UpdateSettingsCommand(plant.Id, Defaults), default);

        f.Saves.Should().Be(0);
        f.Audits.Should().BeEmpty();
        provider.Verify(p => p.Invalidate(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task TC_SET_005_Handler_Refuses_Costing_Change_When_The_Guard_Reports_Open_Orders()
    {
        var f = new Fakes { OpenOrders = true };
        var (handler, _, plant) = Arrange(f);

        var act = () => handler.Handle(new UpdateSettingsCommand(plant.Id, Defaults with { CostingMethod = CostingMethod.Standard }), default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_SET_001");
        f.Audits.Should().BeEmpty();
    }

    // ── The cached provider ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_Provider_Caches_Per_Tenant_And_Plant_And_Reloads_After_Invalidate()
    {
        var f = new Fakes();
        var plant = f.AddPlant();
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(t => t.CurrentTenantId).Returns(Guid.NewGuid());
        var provider = new ProductionSettingProvider(f.SettingRepo, new MemoryCache(new MemoryCacheOptions()), tenant.Object);

        (await provider.GetAsync(plant.Id)).OverIssueTolerancePct.Should().Be(0);
        f.Settings[0].Update(Defaults with { OverIssueTolerancePct = 5 }, false);

        (await provider.GetAsync(plant.Id)).OverIssueTolerancePct.Should().Be(0, "still cached");
        provider.Invalidate(plant.Id);
        (await provider.GetAsync(plant.Id)).OverIssueTolerancePct.Should().Be(5);
    }

    [Fact]
    public async Task Another_Tenants_Cache_Is_Never_Used()
    {
        var f = new Fakes();
        var plant = f.AddPlant();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var t1 = new Mock<ITenantProvider>(); t1.SetupGet(t => t.CurrentTenantId).Returns(Guid.NewGuid());
        var t2 = new Mock<ITenantProvider>(); t2.SetupGet(t => t.CurrentTenantId).Returns(Guid.NewGuid());
        await new ProductionSettingProvider(f.SettingRepo, cache, t1.Object).GetAsync(plant.Id);
        f.Settings[0].Update(Defaults with { OverIssueTolerancePct = 9 }, false);

        (await new ProductionSettingProvider(f.SettingRepo, cache, t2.Object).GetAsync(plant.Id)).OverIssueTolerancePct.Should().Be(9);
    }
}

public class ItemProfileTests
{
    private static ItemProfileValues Values => ItemManufacturingProfile.DefaultValues;

    // ── BR-ITM-03: lot policy (TC-ITM-003..005) ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void TC_ITM_003_FixedQty_Needs_A_Positive_Lot_Quantity(double qty)
    {
        var act = () => ItemManufacturingProfile.Validate(Values with { LotSizingPolicy = LotSizingPolicy.FixedQty, FixedLotQty = (decimal)qty });

        act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("MFG_ITM_002");
    }

    [Fact]
    public void FixedQty_Without_A_Quantity_Is_Refused()
    {
        var act = () => ItemManufacturingProfile.Validate(Values with { LotSizingPolicy = LotSizingPolicy.FixedQty });
        act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("MFG_ITM_002");
    }

    [Fact]
    public void TC_ITM_004_MinMultiple_Needs_A_Positive_Multiple()
    {
        var bad = () => ItemManufacturingProfile.Validate(Values with { LotSizingPolicy = LotSizingPolicy.MinMultiple, MinOrderQty = 1000, OrderMultiple = 0 });
        var ok = () => ItemManufacturingProfile.Validate(Values with { LotSizingPolicy = LotSizingPolicy.MinMultiple, MinOrderQty = 1000, OrderMultiple = 500 });

        bad.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("MFG_ITM_002");
        ok.Should().NotThrow();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(90, true)]
    [InlineData(91, false)]
    public void TC_ITM_005_PeriodOrder_Days_Boundaries(int days, bool valid)
    {
        var act = () => ItemManufacturingProfile.Validate(Values with { LotSizingPolicy = LotSizingPolicy.PeriodOrder, PeriodDays = days });

        if (valid) act.Should().NotThrow(); else act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("MFG_ITM_002");
    }

    [Fact]
    public void Negative_Lead_Time_Or_Safety_Stock_Is_Refused_And_Parameters_Of_Other_Policies_Are_Dropped()
    {
        ((Action)(() => ItemManufacturingProfile.Validate(Values with { FixedLeadTimeDays = -1 }))).Should().Throw<BusinessRuleException>();
        ((Action)(() => ItemManufacturingProfile.Validate(Values with { SafetyStockQty = -1 }))).Should().Throw<BusinessRuleException>();

        var profile = ItemManufacturingProfile.Create(Guid.NewGuid(), Guid.NewGuid(), "X", "X", Values with { LotSizingPolicy = LotSizingPolicy.LotForLot, FixedLotQty = 99, PeriodDays = 5 });
        profile.FixedLotQty.Should().BeNull();
        profile.PeriodDays.Should().BeNull();
    }

    // ── Handler: BR-ITM-01, BR-ITM-04 ────────────────────────────────────────────────────────────────────

    private static (UpsertItemProfileHandler Handler, Plant Plant) Arrange(Fakes f)
    {
        var plant = f.AddPlant();
        var access = new PlantAccessService(f.PlantRepo, f.AccessRepo, new FakeUser(Guid.NewGuid(), PermissionConstants.Manufacturing_SettingsManage));
        return (new UpsertItemProfileHandler(access, f.ProfileRepo, f.ProductClient, f.PlanRepo, f.Uow), plant);
    }

    [Fact]
    public async Task A_Profile_Is_Created_For_An_Active_Item_And_Then_Updated_In_Place()
    {
        var f = new Fakes();
        var (handler, plant) = Arrange(f);
        var item = f.AddProduct("TP-BAN01");

        var created = await handler.Handle(new UpsertItemProfileCommand(item.Id, plant.Id, Values with { FixedLeadTimeDays = 2, SafetyStockQty = 10, RequiresFqc = true }), default);
        var updated = await handler.Handle(new UpsertItemProfileCommand(item.Id, plant.Id, Values with { FixedLeadTimeDays = 3 }), default);

        created.ItemCode.Should().Be("TP-BAN01");
        f.Profiles.Should().ContainSingle();
        updated.Values.FixedLeadTimeDays.Should().Be(3);
        updated.Values.RequiresFqc.Should().BeFalse();
    }

    [Fact]
    public async Task TC_ITM_002_An_Item_That_Is_Not_Active_Or_Has_No_Base_Unit_Or_Does_Not_Exist_Is_Refused()
    {
        var f = new Fakes();
        var (handler, plant) = Arrange(f);
        var inactive = f.AddProduct("OLD", active: false);
        var noUom = f.AddProduct("NOUOM", withUom: false);

        foreach (var id in new[] { inactive.Id, noUom.Id, Guid.NewGuid() })
            await ((Func<Task>)(() => handler.Handle(new UpsertItemProfileCommand(id, plant.Id, Values), default))).Should().ThrowAsync<BusinessRuleException>();

        f.Profiles.Should().BeEmpty();
    }

    [Fact]
    public async Task TC_ITM_006_The_System_Owned_Fields_Survive_An_Update()
    {
        var f = new Fakes();
        var (handler, plant) = Arrange(f);
        var item = f.AddProduct("A");
        await handler.Handle(new UpsertItemProfileCommand(item.Id, plant.Id, Values), default);
        f.Profiles[0].SetLowLevelCode(2);
        f.Profiles[0].SetStandardCost(8000m);

        var after = await handler.Handle(new UpsertItemProfileCommand(item.Id, plant.Id, Values with { FixedLeadTimeDays = 5 }), default);

        after.LowLevelCode.Should().Be(2);
        after.StandardCost.Should().Be(8000m);
    }

    [Fact]
    public async Task Invalid_Lot_Policy_Is_Refused_Before_Product_Service_Is_Called_And_An_Outage_Fails_Closed()
    {
        var f = new Fakes();
        var (handler, plant) = Arrange(f);
        var item = f.AddProduct("A");

        await ((Func<Task>)(() => handler.Handle(new UpsertItemProfileCommand(item.Id, plant.Id, Values with { LotSizingPolicy = LotSizingPolicy.FixedQty }), default)))
            .Should().ThrowAsync<BusinessRuleException>();

        f.Unreachable = true;
        var act = () => handler.Handle(new UpsertItemProfileCommand(item.Id, plant.Id, Values), default);
        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_INT_001");
    }

    // ── GetProfileOrDefault (T0.6.3) ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_Returns_The_Documented_Defaults_When_Nothing_Was_Saved()
    {
        var f = new Fakes();
        var plant = f.AddPlant();
        var access = new PlantAccessService(f.PlantRepo, f.AccessRepo, new FakeUser(Guid.NewGuid(), PermissionConstants.Manufacturing_SettingsManage));

        var dto = await new GetItemProfileHandler(access, f.ProfileRepo).Handle(new GetItemProfileQuery(Guid.NewGuid(), plant.Id), default);

        dto.IsDefault.Should().BeTrue();
        dto.Values.MakeOrBuy.Should().Be(MakeOrBuy.Make);
        dto.Values.LotSizingPolicy.Should().Be(LotSizingPolicy.LotForLot);
        dto.Values.StandardLotSize.Should().Be(1m);
    }
}

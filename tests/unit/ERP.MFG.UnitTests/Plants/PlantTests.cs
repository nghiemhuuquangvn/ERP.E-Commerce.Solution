using ERP.MFG.Application.Features.Plants;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Infrastructure.Services;
using ERP.MFG.UnitTests.Common;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Plants;

public class PlantTests
{
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();

    // ── Domain: BR-PLT-01..03 ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TC_PLT_001_Code_Is_Stored_Upper_Case_So_Lower_Case_Is_The_Same_Plant()
    {
        var plant = Plant.Create(" pl01 ", "Binh Duong", null, A, B, C, null, null);

        plant.Code.Should().Be("PL01");
        Plant.NormalizeCode("pl01").Should().Be(Plant.NormalizeCode("PL01"));
    }

    [Theory]
    [InlineData("P")]
    [InlineData("PL 01")]
    [InlineData("PL_01")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")] // 21
    public void TC_PLT_002_Code_Format_Is_Refused(string code)
    {
        var act = () => Plant.Create(code, "Plant", null, A, B, C, null, null);

        act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("MFG_VALIDATION");
    }

    [Fact]
    public void TC_PLT_003_Quarantine_Warehouse_Is_Required()
    {
        var act = () => Plant.Create("PL01", "Plant", null, A, B, Guid.Empty, null, null);

        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Time_Zone_Defaults_To_Vietnam_And_Unknown_Zones_Are_Refused()
    {
        Plant.Create("PL01", "Plant", null, A, B, C, null, null).TimeZoneId.Should().Be("Asia/Ho_Chi_Minh");
        var act = () => Plant.Create("PL01", "Plant", "Mars/Olympus", A, B, C, null, null);
        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Scrap_Warehouse_And_Calendar_Are_Optional_And_Warehouse_Ids_Are_Distinct()
    {
        var plant = Plant.Create("PL01", "Plant", null, A, A, C, null, null);

        plant.ScrapWarehouseId.Should().BeNull();
        plant.DefaultCalendarId.Should().BeNull();
        plant.WarehouseIds().Should().BeEquivalentTo([A, C]);
    }

    // ── Create: BR-SET-01, BR-PLT-01, BR-PLT-02 ──────────────────────────────────────────────────────────

    private static CreatePlantHandler Create(Fakes f) => new(f.PlantRepo, f.SettingRepo, f.WarehouseClient, f.CalendarRepo, f.Uow);

    private static CreatePlantCommand Command(Fakes f, string code = "PL01") =>
        new(code, "Plant", null, f.AddWarehouse(code + "-RM"), f.AddWarehouse(code + "-FG"), f.AddWarehouse(code + "-QC"), null, null);

    [Fact]
    public async Task TC_SET_001_Creating_A_Plant_Creates_Its_Default_Settings_In_The_Same_Save()
    {
        var f = new Fakes();

        var dto = await Create(f).Handle(Command(f), default);

        f.Plants.Should().ContainSingle();
        f.Settings.Should().ContainSingle().Which.PlantId.Should().Be(dto.Id);
        f.Settings[0].ToValues().Should().Be(ProductionSettingValues.Defaults);
        f.Saves.Should().Be(1, "plant and settings are saved together");
    }

    [Fact]
    public async Task TC_PLT_001_Duplicate_Code_Is_Refused_Ignoring_Case()
    {
        var f = new Fakes();
        await Create(f).Handle(Command(f), default);

        var act = () => Create(f).Handle(Command(f, "pl01"), default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_GEN_006");
        f.Plants.Should().ContainSingle();
    }

    [Fact]
    public async Task A_Warehouse_That_Does_Not_Exist_Or_Is_Inactive_Is_Refused_And_Nothing_Is_Saved()
    {
        var f = new Fakes();
        var missing = new CreatePlantCommand("PL02", "Plant", null, Guid.NewGuid(), f.AddWarehouse("FG"), f.AddWarehouse("QC"), null, null);
        var inactive = new CreatePlantCommand("PL03", "Plant", null, f.AddWarehouse("RM"), f.AddWarehouse("FG2"), f.AddWarehouse("QC2", active: false), null, null);

        await ((Func<Task>)(() => Create(f).Handle(missing, default))).Should().ThrowAsync<BusinessRuleException>();
        await ((Func<Task>)(() => Create(f).Handle(inactive, default))).Should().ThrowAsync<BusinessRuleException>();

        f.Plants.Should().BeEmpty();
        f.Saves.Should().Be(0);
    }

    [Fact]
    public async Task When_The_Warehouse_Service_Cannot_Be_Reached_The_Plant_Is_Not_Created()
    {
        var f = new Fakes { Unreachable = true };

        var act = () => Create(f).Handle(Command(f), default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_INT_001");
        f.Plants.Should().BeEmpty();
    }

    // ── Visibility: BR-GEN-09 (TC-SEC-001) ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC_SEC_001_A_User_Sees_Only_The_Plants_They_Were_Granted()
    {
        var f = new Fakes();
        var pl01 = f.AddPlant("PL01");
        var pl02 = f.AddPlant("PL02");
        var user = Guid.NewGuid();
        f.Access.Add(PlantUserAccess.Grant(pl02.Id, user));
        var service = new PlantAccessService(f.PlantRepo, f.AccessRepo, new FakeUser(user, PermissionConstants.Manufacturing_MasterDataView));

        var list = await new GetPlantsHandler(f.PlantRepo, service).Handle(new GetPlantsQuery(), default);

        list.Select(p => p.Code).Should().Equal("PL02");
        var act = () => service.GetVisiblePlantAsync(pl01.Id);
        await act.Should().ThrowAsync<EntityNotFoundException>("a plant that is not theirs does not exist for them");
    }

    [Fact]
    public async Task A_Settings_Manager_Sees_Every_Plant_Without_Grants()
    {
        var f = new Fakes();
        f.AddPlant("PL01");
        f.AddPlant("PL02");
        var service = new PlantAccessService(f.PlantRepo, f.AccessRepo, new FakeUser(Guid.NewGuid(), PermissionConstants.Manufacturing_SettingsManage));

        (await new GetPlantsHandler(f.PlantRepo, service).Handle(new GetPlantsQuery(), default)).Should().HaveCount(2);
    }

    [Fact]
    public async Task A_User_With_No_Grants_Sees_No_Plant()
    {
        var f = new Fakes();
        f.AddPlant("PL01");
        var service = new PlantAccessService(f.PlantRepo, f.AccessRepo, new FakeUser(Guid.NewGuid(), PermissionConstants.Manufacturing_MasterDataView));

        (await new GetPlantsHandler(f.PlantRepo, service).Handle(new GetPlantsQuery(), default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Granting_Twice_Keeps_One_Row_And_Revoking_Removes_It()
    {
        var f = new Fakes();
        var plant = f.AddPlant();
        var admin = new PlantAccessService(f.PlantRepo, f.AccessRepo, new FakeUser(Guid.NewGuid(), PermissionConstants.Manufacturing_SettingsManage));
        var user = Guid.NewGuid();

        var grant = new GrantPlantAccessHandler(admin, f.AccessRepo, f.Uow);
        await grant.Handle(new GrantPlantAccessCommand(plant.Id, user), default);
        await grant.Handle(new GrantPlantAccessCommand(plant.Id, user), default);
        f.Access.Should().ContainSingle();

        await new RevokePlantAccessHandler(admin, f.AccessRepo, f.Uow).Handle(new RevokePlantAccessCommand(plant.Id, user), default);
        f.Access.Should().BeEmpty();
    }

    // ── R219 (owner decision 2026-11-10): a plant with unfinished orders cannot be deactivated ──────────────────────

    private static SetPlantActiveHandler DeactivationHandler(Fakes f) =>
        new(new PlantAccessService(f.PlantRepo, f.AccessRepo, new FakeUser(Guid.NewGuid(), PermissionConstants.Manufacturing_SettingsManage)), f.Guard, f.Uow);

    [Fact]
    public async Task R219_A_plant_with_unfinished_orders_is_not_deactivated()
    {
        var f = new Fakes { UnfinishedOrders = 3 };
        var plant = f.AddPlant();

        var act = () => DeactivationHandler(f).Handle(new SetPlantActiveCommand(plant.Id, false), default);

        var refusal = (await act.Should().ThrowAsync<BusinessRuleException>()).Which;
        refusal.RuleName.Should().Be(MfgErrors.Plt001);
        refusal.Message.Should().Contain("3").And.Contain("MO-2611-00001");
        plant.IsActive.Should().BeTrue();
        f.Saves.Should().Be(0);
    }

    [Fact]
    public async Task R219_A_plant_with_only_closed_or_cancelled_orders_is_deactivated_and_activation_is_never_blocked()
    {
        var f = new Fakes { UnfinishedOrders = 0 };
        var plant = f.AddPlant();

        (await DeactivationHandler(f).Handle(new SetPlantActiveCommand(plant.Id, false), default)).IsActive.Should().BeFalse();

        f.UnfinishedOrders = 5;                               // activating asks nothing
        (await DeactivationHandler(f).Handle(new SetPlantActiveCommand(plant.Id, true), default)).IsActive.Should().BeTrue();
    }
}

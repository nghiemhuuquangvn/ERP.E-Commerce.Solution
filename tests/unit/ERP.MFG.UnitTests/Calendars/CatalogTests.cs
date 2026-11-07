using ERP.MFG.Application.Features.Crews;
using ERP.MFG.Application.Features.WorkCenters;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Infrastructure.Services;
using ERP.MFG.UnitTests.Common;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Calendars;

/// <summary>Work centers, machines, standard operations, reason codes and crews on the data set DS-01 (BA spec 01 §5.5-5.7).</summary>
public class CatalogTests
{
    private sealed class Ctx
    {
        public Fakes F { get; } = new();
        public Plant Plant { get; }
        public Ds01 D { get; }
        public PlantAccessService Access { get; }

        public Ctx()
        {
            Plant = F.AddPlant();
            D = new Ds01(Plant.Id);
            F.Of<Shift>().AddRange([D.Ca1, D.Ca2, D.Ca3]);
            F.Calendars.Add(D.Cal5D);
            F.WorkingTime.Build = _ => D.Calculator;
            Access = new PlantAccessService(F.PlantRepo, F.AccessRepo, new FakeUser(Guid.NewGuid(), PermissionConstants.Manufacturing_SettingsManage));
        }

        public Task<WorkCenterDto> CreateCenter(string code, WorkCenterValues? values = null)
            => new CreateWorkCenterHandler(Access, F.Catalog, F.CalendarRepo, F.Uow).Handle(new CreateWorkCenterCommand(Plant.Id, code, values ?? Ds01.CenterValues(D.Cal5D.Id)), default);
    }

    private static WorkCenterValues Values(Ctx c, WorkCenterType type = WorkCenterType.Machine, int resources = 1, decimal eff = 100, decimal util = 100, Guid? vendor = null)
        => Ds01.CenterValues(c.D.Cal5D.Id, type, resources, eff, util, 60_000, 120_000, vendor);

    // ── Work centers: BR-WC-01..05 ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC_WC_001_A_Work_Center_Is_Created_With_An_Upper_Case_Code()
    {
        var c = new Ctx();

        var dto = await c.CreateCenter("wc-t01", Values(c));

        dto.Code.Should().Be("WC-T01");
        c.F.Of<WorkCenter>().Should().ContainSingle();
    }

    [Fact]
    public async Task TC_WC_002_The_Same_Code_Twice_In_A_Plant_Is_Refused()
    {
        var c = new Ctx();
        await c.CreateCenter("WC-CUT", Values(c));

        var act = () => c.CreateCenter("wc-cut", Values(c));

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_GEN_006");
    }

    [Theory]
    [InlineData("W")]
    [InlineData("WC_CUT")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    public async Task TC_WC_003_A_Badly_Formed_Code_Is_Refused_With_The_Spec_Code(string code)
    {
        var c = new Ctx();

        var act = () => c.CreateCenter(code, Values(c));

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_WC_001");
    }

    [Theory]
    [InlineData("2026-11-09", 1620)]   // Monday: 900 x 2 x 100% x 90%
    [InlineData("2026-11-14", 810)]    // Saturday
    [InlineData("2027-01-01", 0)]      // holiday
    public async Task TC_WC_004_Daily_Capacity_Of_The_Cutting_Center(string date, int expected)
    {
        var c = new Ctx();
        var cut = await c.CreateCenter("WC-CUT", Values(c, resources: 2, util: 90));

        var days = await new CapacityService(c.F.Catalog, c.F.WorkingTime).GetDailyCapacityAsync(cut.Id, DateOnly.Parse(date), DateOnly.Parse(date));

        days.Should().ContainSingle().Which.CapacityMinutes.Should().Be(expected);
    }

    [Fact]
    public async Task TC_WC_005_Efficiency_Lowers_Capacity()
    {
        var c = new Ctx();
        var paint = await c.CreateCenter("WC-PNT", Values(c, resources: 1, eff: 80));

        var days = await new CapacityService(c.F.Catalog, c.F.WorkingTime).GetDailyCapacityAsync(paint.Id, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 9));

        days.Single().CapacityMinutes.Should().Be(720m); // 900 x 1 x 0.8 x 1.0
    }

    [Fact]
    public async Task The_Capacity_Chart_Lists_Every_Day_Of_The_Range_And_Refuses_Absurd_Ranges()
    {
        var c = new Ctx();
        var wc = await c.CreateCenter("WC-ASM", Values(c));
        var service = new CapacityService(c.F.Catalog, c.F.WorkingTime);

        (await service.GetDailyCapacityAsync(wc.Id, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 15))).Select(d => d.WorkingMinutes).Should().Equal(900, 900, 900, 900, 900, 450, 0);
        await ((Func<Task>)(() => service.GetDailyCapacityAsync(wc.Id, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 1)))).Should().ThrowAsync<BusinessRuleException>();
        await ((Func<Task>)(() => service.GetDailyCapacityAsync(wc.Id, new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1)))).Should().ThrowAsync<BusinessRuleException>();
    }

    [Theory]
    [InlineData(0, 100, 100, 1, false)]
    [InlineData(1, 100, 100, 1, true)]
    [InlineData(200, 100, 100, 1, true)]
    [InlineData(201, 100, 100, 1, false)]
    [InlineData(100, 0, 100, 1, false)]
    [InlineData(100, 100, 100, 1, true)]
    [InlineData(100, 101, 100, 1, false)]
    [InlineData(100, 100, 100, 0, false)]
    [InlineData(100, 100, 100, 999, true)]
    [InlineData(100, 100, 100, 1000, false)]
    public void TC_WC_006_Efficiency_Utilization_And_Resource_Boundaries(double eff, double util, double _, int resources, bool valid)
    {
        var calendar = Guid.NewGuid();
        var act = () => WorkCenter.Create(Guid.NewGuid(), "WC-X", Ds01.CenterValues(calendar, resources: resources, eff: (decimal)eff, util: (decimal)util));

        if (valid) act.Should().NotThrow();
        else act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("MFG_WC_002");
    }

    [Fact]
    public void A_Negative_Rate_Is_Refused()
        => ((Action)(() => WorkCenter.Create(Guid.NewGuid(), "WC-X", Ds01.CenterValues(Guid.NewGuid(), labor: -1)))).Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("MFG_WC_002");

    [Fact]
    public async Task TC_WC_007_A_Work_Center_With_Open_Operations_Cannot_Be_Deactivated()
    {
        var c = new Ctx { };
        var wc = await c.CreateCenter("WC-ASM", Values(c));
        c.F.WorkCenterBusy = true;

        var act = () => new SetWorkCenterActiveHandler(c.Access, c.F.Catalog, c.F.Guard, c.F.Uow).Handle(new SetWorkCenterActiveCommand(wc.Id, false), default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_WC_003");
        c.F.Of<WorkCenter>().Single().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task TC_WC_008_A_Work_Center_Without_Open_Operations_Can_Be_Deactivated_And_Reactivated()
    {
        var c = new Ctx();
        var wc = await c.CreateCenter("WC-T01", Values(c));
        var handler = new SetWorkCenterActiveHandler(c.Access, c.F.Catalog, c.F.Guard, c.F.Uow);

        (await handler.Handle(new SetWorkCenterActiveCommand(wc.Id, false), default)).IsActive.Should().BeFalse();
        c.F.WorkCenterBusy = true;
        (await handler.Handle(new SetWorkCenterActiveCommand(wc.Id, true), default)).IsActive.Should().BeTrue("activating is never blocked");
    }

    [Fact]
    public async Task TC_WC_009_A_Subcontract_Center_Needs_A_Vendor()
    {
        var c = new Ctx();

        var without = () => c.CreateCenter("WC-SUB", Values(c, WorkCenterType.Subcontract));
        (await without.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_WC_004");

        var created = await c.CreateCenter("WC-SUB", Values(c, WorkCenterType.Subcontract, vendor: Guid.NewGuid()));
        created.Values.VendorId.Should().NotBeNull();
    }

    [Fact]
    public async Task A_Work_Center_Needs_A_Calendar_Of_Its_Own_Plant_That_Is_Active()
    {
        var c = new Ctx();
        var foreign = WorkCalendar.Create(Guid.NewGuid(), "OTHER", "Other plant");
        c.F.Calendars.Add(foreign);
        c.D.Cal5D.SetActive(false);

        await ((Func<Task>)(() => c.CreateCenter("WC-A", Ds01.CenterValues(Guid.NewGuid())))).Should().ThrowAsync<BusinessRuleException>("unknown calendar");
        await ((Func<Task>)(() => c.CreateCenter("WC-B", Ds01.CenterValues(foreign.Id)))).Should().ThrowAsync<BusinessRuleException>("calendar of another plant");
        await ((Func<Task>)(() => c.CreateCenter("WC-C", Values(c)))).Should().ThrowAsync<BusinessRuleException>("inactive calendar");
    }

    [Fact]
    public async Task TC_GEN_003_004_A_Used_Work_Center_Cannot_Be_Deleted_An_Unused_One_Is_Soft_Deleted()
    {
        var c = new Ctx();
        var used = await c.CreateCenter("WC-ASM", Values(c));
        var spare = await c.CreateCenter("WC-T01", Values(c));
        await new CreateMachineHandler(c.Access, c.F.Catalog, c.F.Uow).Handle(new CreateMachineCommand(c.Plant.Id, "M-1", "Machine", used.Id, 60, null), default);
        var delete = new DeleteWorkCenterHandler(c.Access, c.F.Catalog, c.F.References, c.F.Uow);

        var act = () => delete.Handle(new DeleteWorkCenterCommand(used.Id), default);
        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_GEN_005");

        await delete.Handle(new DeleteWorkCenterCommand(spare.Id), default);
        (await new GetWorkCentersHandler(c.Access, c.F.Catalog).Handle(new GetWorkCentersQuery(c.Plant.Id), default)).Select(w => w.Code).Should().Equal("WC-ASM");
    }

    [Fact]
    public async Task A_Work_Center_Of_A_Plant_The_User_May_Not_See_Does_Not_Exist_For_Them()
    {
        var c = new Ctx();
        var wc = await c.CreateCenter("WC-ASM", Values(c));
        var outsider = new PlantAccessService(c.F.PlantRepo, c.F.AccessRepo, new FakeUser(Guid.NewGuid(), PermissionConstants.Manufacturing_MasterDataView));

        var act = () => new GetWorkCenterHandler(outsider, c.F.Catalog).Handle(new GetWorkCenterQuery(wc.Id), default);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    // ── Machines: BR-MCH-01/02 ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC_MCH_001_002_A_Machine_Needs_A_Work_Center_Of_The_Plant_And_A_Positive_Cycle_Time()
    {
        var c = new Ctx();
        var cut = await c.CreateCenter("WC-CUT", Values(c));
        var create = new CreateMachineHandler(c.Access, c.F.Catalog, c.F.Uow);

        (await create.Handle(new CreateMachineCommand(c.Plant.Id, "M-CUT-03", "Cutter 3", cut.Id, 60, null), default)).Status.Should().Be(MachineStatus.Active);
        await ((Func<Task>)(() => create.Handle(new CreateMachineCommand(c.Plant.Id, "M-CUT-04", "Cutter 4", cut.Id, 0, null), default))).Should().ThrowAsync<BusinessRuleException>();
        await ((Func<Task>)(() => create.Handle(new CreateMachineCommand(c.Plant.Id, "M-CUT-05", "Cutter 5", Guid.NewGuid(), 60, null), default))).Should().ThrowAsync<BusinessRuleException>();
        await ((Func<Task>)(() => create.Handle(new CreateMachineCommand(c.Plant.Id, "m-cut-03", "Duplicate", cut.Id, 60, null), default))).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public void TC_MCH_003_A_Retired_Machine_Cannot_Be_Chosen()
    {
        var machine = Machine.Create(Guid.NewGuid(), "M-OLD-01", "Old", Guid.NewGuid(), 60, null);
        machine.SetStatus(MachineStatus.Retired);

        var act = () => machine.AssertUsable();

        act.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("MFG_OP_010");
    }

    [Fact]
    public void TC_MCH_004_A_Machine_Under_Maintenance_Can_Be_Chosen_With_A_Warning()
    {
        var machine = Machine.Create(Guid.NewGuid(), "M-CUT-02", "Cutter", Guid.NewGuid(), 60, null);
        machine.AssertUsable().Should().BeFalse();

        machine.SetStatus(MachineStatus.UnderMaintenance);

        machine.AssertUsable().Should().BeTrue("the caller shows a warning");
    }

    // ── Standard operations: BR-OPS-01 ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC_OPS_002_A_Standard_Operation_Code_Is_Unique_In_The_Plant_And_The_Times_Are_Checked()
    {
        var c = new Ctx();
        var wc = await c.CreateCenter("WC-CUT", Values(c));
        var create = new CreateStandardOperationHandler(c.Access, c.F.Catalog, c.F.Uow);
        var body = new StandardOperationBody("Cut", wc.Id, 20, 3, RunTimeBasis.PerUnit, null, "Follow the drawing");

        var first = await create.Handle(new CreateStandardOperationCommand(c.Plant.Id, "cat-01", body), default);
        first.Code.Should().Be("CAT-01");

        (await ((Func<Task>)(() => create.Handle(new CreateStandardOperationCommand(c.Plant.Id, "CAT-01", body), default))).Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_GEN_006");
        (await ((Func<Task>)(() => create.Handle(new CreateStandardOperationCommand(c.Plant.Id, "CAT-02", body with { RunTimeMinutes = 0 }), default))).Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_RT_002");
        (await ((Func<Task>)(() => create.Handle(new CreateStandardOperationCommand(c.Plant.Id, "CAT-03", body with { SetupTimeMinutes = -1 }), default))).Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_RT_002");
        (await ((Func<Task>)(() => create.Handle(new CreateStandardOperationCommand(c.Plant.Id, "CAT-04", body with { RunTimeBasis = RunTimeBasis.PerBatch }), default))).Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_RT_002");
    }

    [Fact]
    public async Task A_Per_Batch_Operation_Keeps_Its_Batch_Size_And_A_Per_Unit_One_Drops_It()
    {
        var c = new Ctx();
        var wc = await c.CreateCenter("WC-CUT", Values(c));
        var create = new CreateStandardOperationHandler(c.Access, c.F.Catalog, c.F.Uow);

        var perBatch = await create.Handle(new CreateStandardOperationCommand(c.Plant.Id, "BAT-01", new("Batch", wc.Id, 10, 30, RunTimeBasis.PerBatch, 40, null)), default);
        var perUnit = await create.Handle(new CreateStandardOperationCommand(c.Plant.Id, "UNI-01", new("Unit", wc.Id, 10, 30, RunTimeBasis.PerUnit, 40, null)), default);

        perBatch.BatchSize.Should().Be(40);
        perUnit.BatchSize.Should().BeNull();
    }

    // ── Reason codes: BR-RSN-01/02 ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC_RSN_001_The_Default_Set_Has_11_Reasons_With_The_Right_Type_And_Category_And_Is_Seeded_Once()
    {
        var f = new Fakes();

        var added = await new SeedReasonCodesHandler(f.ReasonRepo, f.Uow).Handle(new SeedReasonCodesCommand(), default);
        var again = await new SeedReasonCodesHandler(f.ReasonRepo, f.Uow).Handle(new SeedReasonCodesCommand(), default);

        added.Should().Be(11);
        again.Should().Be(0);
        f.Reasons.Should().HaveCount(11);
        f.Reasons.Single(r => r.Code == "DT-01").Should().Match<ReasonCode>(r => r.Type == ReasonType.Downtime && r.Category == DowntimeCategory.Unplanned);
        f.Reasons.Single(r => r.Code == "DT-02").Category.Should().Be(DowntimeCategory.Planned);
        f.Reasons.Single(r => r.Code == "SCRAP-01").Should().Match<ReasonCode>(r => r.Type == ReasonType.Scrap && r.Category == null);
        f.Reasons.Select(r => r.Type).Distinct().Should().HaveCount(8, "every reason type has at least one default");
    }

    [Fact]
    public async Task A_Tenant_That_Has_No_Reasons_Gets_The_Default_Set_On_Its_First_Read()
    {
        var f = new Fakes();

        var list = await new GetReasonCodesHandler(f.ReasonRepo, f.Uow).Handle(new GetReasonCodesQuery(), default);
        var downtime = await new GetReasonCodesHandler(f.ReasonRepo, f.Uow).Handle(new GetReasonCodesQuery(ReasonType.Downtime), default);

        list.Should().HaveCount(11);
        downtime.Select(r => r.Code).Should().Equal("DT-01", "DT-02", "DT-03");
        f.Saves.Should().Be(1, "seeded once");
    }

    [Fact]
    public async Task TC_RSN_002_The_Code_Is_Unique_Per_Type_Not_Across_Types()
    {
        var f = new Fakes();
        var create = new CreateReasonCodeHandler(f.ReasonRepo, f.Uow);
        await create.Handle(new CreateReasonCodeCommand(ReasonType.Scrap, "SCRAP-01", "Material", null), default);

        var same = () => create.Handle(new CreateReasonCodeCommand(ReasonType.Scrap, "scrap-01", "Again", null), default);
        (await same.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_GEN_006");

        var other = await create.Handle(new CreateReasonCodeCommand(ReasonType.Rework, "SCRAP-01", "Reuse of the code", null), default);
        other.Type.Should().Be(ReasonType.Rework);
    }

    [Fact]
    public void TC_RSN_003_A_Downtime_Reason_Needs_Planned_Or_Unplanned_And_Other_Types_Have_No_Category()
    {
        ((Action)(() => ReasonCode.Create(ReasonType.Downtime, "DT-09", "No category", null))).Should().Throw<BusinessRuleException>();
        ReasonCode.Create(ReasonType.Scrap, "SCRAP-09", "Ignores category", DowntimeCategory.Planned).Category.Should().BeNull();
    }

    // ── Crews: BR-LBR-01/02 ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC_LBR_001_An_Employee_Cannot_Be_In_Two_Crews_At_The_Same_Time()
    {
        var c = new Ctx();
        var nv001 = c.F.AddEmployee("NV001");
        var create = new CreateCrewHandler(c.Access, c.F.Catalog, c.F.EmployeeClient, c.F.Uow);
        var t1 = await create.Handle(new CreateCrewCommand(c.Plant.Id, "T1", "Team 1", null, null), default);
        var t2 = await create.Handle(new CreateCrewCommand(c.Plant.Id, "T2", "Team 2", null, null), default);
        var add = new AddCrewMemberHandler(c.Access, c.F.CrewRepo, c.F.EmployeeClient, c.F.Uow);

        await add.Handle(new AddCrewMemberCommand(t1.Id, nv001, new DateOnly(2026, 1, 1), null, null), default);

        var overlap = () => add.Handle(new AddCrewMemberCommand(t2.Id, nv001, new DateOnly(2026, 6, 1), null, null), default);
        await overlap.Should().ThrowAsync<BusinessRuleException>();
        c.F.Crews.Single(x => x.Code == "T2").Members.Should().BeEmpty();
    }

    [Fact]
    public async Task A_Move_Between_Crews_Is_Fine_When_The_Periods_Do_Not_Overlap()
    {
        var c = new Ctx();
        var nv = c.F.AddEmployee("NV002");
        var create = new CreateCrewHandler(c.Access, c.F.Catalog, c.F.EmployeeClient, c.F.Uow);
        var t1 = await create.Handle(new CreateCrewCommand(c.Plant.Id, "T1", "Team 1", null, null), default);
        var t2 = await create.Handle(new CreateCrewCommand(c.Plant.Id, "T2", "Team 2", null, null), default);
        var add = new AddCrewMemberHandler(c.Access, c.F.CrewRepo, c.F.EmployeeClient, c.F.Uow);

        await add.Handle(new AddCrewMemberCommand(t1.Id, nv, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), null), default);
        var moved = await add.Handle(new AddCrewMemberCommand(t2.Id, nv, new DateOnly(2026, 7, 1), null, null), default);

        moved.Members.Should().ContainSingle().Which.EmployeeNumber.Should().Be("NV002");
    }

    [Fact]
    public async Task An_Unknown_Or_Terminated_Employee_Is_Refused_As_Member_Or_Leader()
    {
        var c = new Ctx();
        var gone = c.F.AddEmployee("NV900", active: false);
        var create = new CreateCrewHandler(c.Access, c.F.Catalog, c.F.EmployeeClient, c.F.Uow);
        var crew = await create.Handle(new CreateCrewCommand(c.Plant.Id, "T1", "Team 1", null, null), default);
        var add = new AddCrewMemberHandler(c.Access, c.F.CrewRepo, c.F.EmployeeClient, c.F.Uow);

        await ((Func<Task>)(() => add.Handle(new AddCrewMemberCommand(crew.Id, Guid.NewGuid(), new DateOnly(2026, 1, 1), null, null), default))).Should().ThrowAsync<BusinessRuleException>();
        await ((Func<Task>)(() => add.Handle(new AddCrewMemberCommand(crew.Id, gone, new DateOnly(2026, 1, 1), null, null), default))).Should().ThrowAsync<BusinessRuleException>();
        await ((Func<Task>)(() => create.Handle(new CreateCrewCommand(c.Plant.Id, "T2", "Team 2", null, gone), default))).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task A_Membership_Cannot_End_Before_It_Starts_And_A_Rate_Cannot_Be_Negative()
    {
        var c = new Ctx();
        var nv = c.F.AddEmployee("NV003");
        var crew = await new CreateCrewHandler(c.Access, c.F.Catalog, c.F.EmployeeClient, c.F.Uow).Handle(new CreateCrewCommand(c.Plant.Id, "T1", "Team 1", null, null), default);
        var add = new AddCrewMemberHandler(c.Access, c.F.CrewRepo, c.F.EmployeeClient, c.F.Uow);

        await ((Func<Task>)(() => add.Handle(new AddCrewMemberCommand(crew.Id, nv, new DateOnly(2026, 5, 1), new DateOnly(2026, 4, 1), null), default))).Should().ThrowAsync<BusinessRuleException>();
        await ((Func<Task>)(() => add.Handle(new AddCrewMemberCommand(crew.Id, nv, new DateOnly(2026, 5, 1), null, -5), default))).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task TC_LBR_002_003_An_Own_Rate_Beats_The_Work_Center_Rate_And_Without_One_The_Work_Center_Rate_Applies()
    {
        var c = new Ctx();
        var asm = await c.CreateCenter("WC-ASM", Ds01.CenterValues(c.D.Cal5D.Id, WorkCenterType.Labor, labor: 50_000));
        var nv001 = c.F.AddEmployee("NV001");
        var nv002 = c.F.AddEmployee("NV002");
        var crew = await new CreateCrewHandler(c.Access, c.F.Catalog, c.F.EmployeeClient, c.F.Uow).Handle(new CreateCrewCommand(c.Plant.Id, "T1", "Team 1", asm.Id, null), default);
        var add = new AddCrewMemberHandler(c.Access, c.F.CrewRepo, c.F.EmployeeClient, c.F.Uow);
        await add.Handle(new AddCrewMemberCommand(crew.Id, nv001, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 70_000), default);
        await add.Handle(new AddCrewMemberCommand(crew.Id, nv002, new DateOnly(2026, 1, 1), null, null), default);
        var resolver = new LaborRateResolver(c.F.CrewRepo, c.F.Catalog);

        (await resolver.GetRateAsync(nv001, asm.Id, new DateOnly(2026, 11, 9))).Should().Be(70_000m);
        (await resolver.GetRateAsync(nv002, asm.Id, new DateOnly(2026, 11, 9))).Should().Be(50_000m);
        (await resolver.GetRateAsync(nv001, asm.Id, new DateOnly(2027, 2, 1))).Should().Be(50_000m, "the own rate was only valid in 2026");
    }

    [Fact]
    public async Task The_Crew_List_Shows_Member_Numbers_And_Names()
    {
        var c = new Ctx();
        var nv = c.F.AddEmployee("NV007");
        var crew = await new CreateCrewHandler(c.Access, c.F.Catalog, c.F.EmployeeClient, c.F.Uow).Handle(new CreateCrewCommand(c.Plant.Id, "T1", "Team 1", null, null), default);
        await new AddCrewMemberHandler(c.Access, c.F.CrewRepo, c.F.EmployeeClient, c.F.Uow).Handle(new AddCrewMemberCommand(crew.Id, nv, new DateOnly(2026, 1, 1), null, null), default);

        var list = await new GetCrewsHandler(c.Access, c.F.CrewRepo, c.F.EmployeeClient).Handle(new GetCrewsQuery(c.Plant.Id), default);

        list.Single().Members.Single().EmployeeNumber.Should().Be("NV007");
    }

    // ── The plant's default calendar (BR-PLT-03) ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_Plants_Default_Calendar_Must_Be_A_Calendar_Of_That_Plant()
    {
        var c = new Ctx();
        var foreign = WorkCalendar.Create(Guid.NewGuid(), "OTHER", "Other plant");
        c.F.Calendars.Add(foreign);
        var handler = new ERP.MFG.Application.Features.Plants.UpdatePlantHandler(c.Access, c.F.WarehouseClient, c.F.CalendarRepo, c.F.Uow);
        var p = c.Plant;

        var ok = await handler.Handle(new(p.Id, p.Name, null, p.RawMaterialWarehouseId, p.FinishedGoodsWarehouseId, p.QuarantineWarehouseId, null, c.D.Cal5D.Id), default);
        ok.DefaultCalendarId.Should().Be(c.D.Cal5D.Id);

        await ((Func<Task>)(() => handler.Handle(new(p.Id, p.Name, null, p.RawMaterialWarehouseId, p.FinishedGoodsWarehouseId, p.QuarantineWarehouseId, null, foreign.Id), default))).Should().ThrowAsync<BusinessRuleException>();
    }
}

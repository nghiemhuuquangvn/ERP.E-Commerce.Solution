using ERP.MFG.Application.Features.Boms;
using ERP.MFG.Application.Features.Routings;
using ERP.MFG.Application.Services;
using ERP.MFG.UnitTests.Common;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Domain.Scheduling;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Boms;

/// <summary>Routings, operation times, lead time over the calendars and the BOM / routing check on the data set DS-01 (BR-RT-01..08).</summary>
public class RoutingTests
{
    private static async Task<BusinessRuleException> Refused(Func<Task> action)
        => (await FluentActions.Awaiting(action).Should().ThrowAsync<BusinessRuleException>()).Which;

    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    private static async Task<(RoutingCtx Ctx, RoutingDto Draft)> WithDraft()
    {
        var c = new RoutingCtx();
        c.SeedItems();
        return (c, await c.NewRoutingAsync("TP-BAN01"));
    }

    // ── Operations: BR-RT-01, -02, -06 ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC_RT_001_An_Operation_Number_Cannot_Be_Used_Twice()
    {
        var (c, r) = await WithDraft();
        await c.AddOpAsync(r.Id, 20, "Painting", "WC-PNT", 30, 6);

        var refusal = await Refused(() => c.AddOpAsync(r.Id, 20, "Again", "WC-PNT", 30, 6));

        refusal.RuleName.Should().Be(MfgErrors.Rt001);
        refusal.Message.Should().Contain("20");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task TC_RT_002_An_Operation_Number_Must_Be_Positive(int seq)
    {
        var (c, r) = await WithDraft();

        (await Refused(() => c.AddOpAsync(r.Id, seq, "Bad", "WC-ASM", 0, 1))).RuleName.Should().Be(MfgErrors.Rt001);
    }

    [Theory]
    [InlineData(0, 0, "PerUnit", null)]      // run 0
    [InlineData(-1, 5, "PerUnit", null)]     // setup -1
    [InlineData(0, 5, "PerBatch", 0.0)]      // batch 0
    [InlineData(0, 5, "PerBatch", null)]     // batch missing
    public async Task TC_RT_003_Times_And_Batch_Must_Be_Valid(double setup, double run, string basis, double? batch)
    {
        var (c, r) = await WithDraft();

        var refusal = await Refused(() => c.AddOpAsync(r.Id, 10, "Bad", "WC-ASM", (decimal)setup, (decimal)run, Enum.Parse<RunTimeBasis>(basis), (decimal?)batch));

        refusal.RuleName.Should().Be(MfgErrors.Rt002);
    }

    [Fact]
    public async Task TC_RT_004_A_Work_Center_Of_Another_Plant_Or_One_That_Is_Not_Active_Is_Refused()
    {
        var (c, r) = await WithDraft();
        var other = WorkCenter.Create(Guid.NewGuid(), "WC-PL02", Ds01.CenterValues(c.D.Cal5D.Id));
        c.F.Of<WorkCenter>().Add(other);
        c.Centers["WC-PNT"].SetActive(false, hasOpenOperations: false);

        (await Refused(() => c.AddOpAsync(r.Id, Op(10, other.Id)))).RuleName.Should().Be(MfgErrors.Rt003);
        (await Refused(() => c.AddOpAsync(r.Id, Op(10, c.Centers["WC-PNT"].Id)))).RuleName.Should().Be(MfgErrors.Rt003);
        (await Refused(() => c.AddOpAsync(r.Id, Op(10, Guid.NewGuid())))).RuleName.Should().Be(MfgErrors.Rt003);

        static RoutingOperationBody Op(int seq, Guid center) => RoutingCtx.Op(seq, "Op", center, 0, 1);
    }

    [Fact]
    public async Task TC_RT_009_A_Subcontracted_Operation_Needs_A_Subcontract_Work_Center()
    {
        var (c, r) = await WithDraft();

        var refusal = await Refused(() => c.AddOpAsync(r.Id, RoutingCtx.Op(10, "Outside", c.Centers["WC-ASM"].Id, 0, 1, subcontract: true)));
        refusal.RuleName.Should().Be(MfgErrors.Rt004);

        var ok = await c.AddOpAsync(r.Id, RoutingCtx.Op(10, "Outside", c.Centers["WC-OUT"].Id, 0, 1, subcontract: true));
        ok.Operations.Single().IsSubcontract.Should().BeTrue();
    }

    [Fact]
    public async Task An_Operation_Without_A_Number_Gets_The_Next_Ten_And_Without_A_Name_Gets_One()
    {
        var (c, r) = await WithDraft();
        await c.AddOpAsync(r.Id, RoutingCtx.Op(null, "", c.Centers["WC-ASM"].Id, 0, 1));
        var after = await c.AddOpAsync(r.Id, RoutingCtx.Op(null, "Second", c.Centers["WC-PNT"].Id, 0, 1));

        after.Operations.Select(o => o.OperationSeq).Should().Equal(10, 20);
        after.Operations[0].Name.Should().Be("Operation 10");
    }

    [Fact]
    public async Task An_Operation_From_A_Standard_Operation_Copies_Its_Values_And_Only_Remembers_The_Link()
    {
        var (c, r) = await WithDraft();
        var standard = c.Standard("PNT-STD", "WC-PNT", 30, 6);

        var added = await new AddRoutingOperationHandler(c.Access, c.F.RoutingRepo, c.F.Catalog, c.F.PlanRepo, c.F.Uow)
            .Handle(new AddRoutingOperationCommand(r.Id, new RoutingOperationBody(null, standard.Id, null, null, null, null, null, null, null, null, false, null)), default);

        var op = added.Operations.Single();
        op.Should().Match<RoutingOperationDto>(o => o.WorkCenterCode == "WC-PNT" && o.SetupTimeMinutes == 30 && o.RunTimeMinutes == 6 && o.Name == "Standard PNT-STD" && o.StandardOperationId == standard.Id);
        op.Instructions.Should().Be("Do it well");
    }

    [Fact]
    public async Task Changing_The_Standard_Operation_Later_Does_Not_Change_The_Routing()
    {
        var (c, r) = await WithDraft();
        var standard = c.Standard("PNT-STD", "WC-PNT", 30, 6);
        await new AddRoutingOperationHandler(c.Access, c.F.RoutingRepo, c.F.Catalog, c.F.PlanRepo, c.F.Uow)
            .Handle(new AddRoutingOperationCommand(r.Id, new RoutingOperationBody(null, standard.Id, null, null, null, null, null, null, null, null, false, null)), default);

        standard.Update("Changed", standard.DefaultWorkCenterId, 99, 99, RunTimeBasis.PerUnit, null, null);

        c.F.Routings.Single().Operations.Single().SetupTimeMinutes.Should().Be(30);
    }

    [Fact]
    public async Task Operations_Are_Renumbered_In_The_Order_Of_The_Screen()
    {
        var (c, r) = await WithDraft();
        await c.AddOpAsync(r.Id, 10, "A", "WC-ASM", 0, 1);
        await c.AddOpAsync(r.Id, 15, "B", "WC-PNT", 0, 1);
        await c.AddOpAsync(r.Id, 40, "C", "WC-PKG", 0, 1);

        var renumbered = await new RenumberRoutingHandler(c.Access, c.F.RoutingRepo, c.F.Uow).Handle(new RenumberRoutingCommand(r.Id, [40, 10, 15]), default);

        renumbered.Operations.Select(o => (o.OperationSeq, o.Name)).Should().Equal((10, "C"), (20, "A"), (30, "B"));
    }

    [Fact]
    public async Task A_New_Order_Must_List_Every_Operation_Once()
    {
        var (c, r) = await WithDraft();
        await c.AddOpAsync(r.Id, 10, "A", "WC-ASM", 0, 1);
        await c.AddOpAsync(r.Id, 20, "B", "WC-PNT", 0, 1);
        var handler = new RenumberRoutingHandler(c.Access, c.F.RoutingRepo, c.F.Uow);

        await Refused(() => handler.Handle(new RenumberRoutingCommand(r.Id, [10]), default));
        await Refused(() => handler.Handle(new RenumberRoutingCommand(r.Id, [10, 10]), default));
        await Refused(() => handler.Handle(new RenumberRoutingCommand(r.Id, [10, 99]), default));
    }

    // ── Versions: BR-RT-03 ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC_RT_005_Approving_A_Later_Version_Ends_The_Open_One_The_Day_Before()
    {
        var c = new RoutingCtx();
        c.SeedItems();
        var v1 = await c.SeedTopRoutingAsync();
        var v2 = await c.CopyRoutingAsync(v1.Id, D(2026, 12, 1));

        v2.Version.Should().Be(2);
        v2.Operations.Should().HaveCount(3);
        await c.ApproveRoutingAsync(v2.Id);

        c.F.Routings.Single(x => x.Id == v1.Id).EffectiveTo.Should().Be(D(2026, 11, 30));
        new RoutingResolver(c.F.RoutingRepo).GetEffectiveRoutingAsync(c.Items["TP-BAN01"].Id, c.Plant.Id, D(2026, 12, 1)).Result!.Version.Should().Be(2);
    }

    [Fact]
    public async Task A_Version_That_Overlaps_A_Closed_Period_Is_Refused_And_Changes_Nothing()
    {
        var c = new RoutingCtx();
        c.SeedItems();
        var v1 = await c.NewRoutingAsync("TP-BAN01", D(2026, 1, 1), D(2026, 12, 31));
        await c.AddOpAsync(v1.Id, 10, "A", "WC-ASM", 0, 1);
        await c.ApproveRoutingAsync(v1.Id);
        var v2 = await c.CopyRoutingAsync(v1.Id, D(2026, 6, 1));

        (await Refused(() => c.ApproveRoutingAsync(v2.Id))).RuleName.Should().Be(MfgErrors.Bom003);
        c.F.Routings.Single(x => x.Id == v2.Id).Status.Should().Be(VersionStatus.Draft);
    }

    [Fact]
    public async Task An_Approved_Routing_Cannot_Be_Edited_Or_Deleted()
    {
        var c = new RoutingCtx();
        c.SeedItems();
        var v1 = await c.SeedTopRoutingAsync();

        (await Refused(() => c.AddOpAsync(v1.Id, 40, "More", "WC-ASM", 0, 1))).RuleName.Should().Be(MfgErrors.Bom002);
        (await Refused(() => new DeleteRoutingHandler(c.Access, c.F.RoutingRepo, c.F.Uow).Handle(new DeleteRoutingCommand(v1.Id), default))).RuleName.Should().Be(MfgErrors.Bom002);
    }

    [Fact]
    public async Task A_Routing_Without_Operations_Cannot_Be_Approved()
    {
        var (c, r) = await WithDraft();

        await FluentActions.Awaiting(() => c.ApproveRoutingAsync(r.Id)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task A_Work_Center_Deactivated_After_The_Draft_Was_Built_Blocks_Approval()
    {
        var (c, r) = await WithDraft();
        await c.AddOpAsync(r.Id, 10, "A", "WC-ASM", 0, 1);
        c.Centers["WC-ASM"].SetActive(false, hasOpenOperations: false);

        (await Refused(() => c.ApproveRoutingAsync(r.Id))).RuleName.Should().Be(MfgErrors.Rt003);
    }

    [Fact]
    public async Task TC_RT_010_An_Item_Without_A_Routing_Simply_Has_None()
    {
        var c = new RoutingCtx();
        await c.SeedBomsAsync();

        (await new RoutingResolver(c.F.RoutingRepo).GetEffectiveRoutingAsync(c.Items["BTP-CHAN01"].Id, c.Plant.Id, BomCtx.Nov4)).Should().BeNull();
        await FluentActions.Awaiting(() => new GetEffectiveRoutingHandler(c.Access, new RoutingResolver(c.F.RoutingRepo))
            .Handle(new GetEffectiveRoutingQuery(c.Plant.Id, c.Items["BTP-CHAN01"].Id, BomCtx.Nov4), default)).Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task A_Routing_Of_Another_Plant_Does_Not_Exist_For_A_User_Who_Cannot_See_It()
    {
        var c = new RoutingCtx();
        c.SeedItems();
        var v1 = await c.SeedTopRoutingAsync();
        var outsider = new ERP.MFG.Infrastructure.Services.PlantAccessService(c.F.PlantRepo, c.F.AccessRepo, new ERP.MFG.UnitTests.Common.FakeUser(Guid.NewGuid()));

        await FluentActions.Awaiting(() => new GetRoutingHandler(outsider, c.F.RoutingRepo).Handle(new GetRoutingQuery(v1.Id), default)).Should().ThrowAsync<EntityNotFoundException>();
    }

    // ── Times: BR-RT-04 ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TC_RT_006_Painting_100_Pieces_Is_630_Standard_And_788_On_The_Calendar_At_80_Percent()
    {
        var standard = OperationTimeCalculator.StandardMinutes(30, 6, RunTimeBasis.PerUnit, null, 100);

        standard.Should().Be(630m);
        OperationTimeCalculator.CalendarMinutes(standard, 80).Should().Be(788, "630 / 0.8 = 787.5, rounded up");
    }

    [Fact]
    public void TC_RT_007_A_Batch_Operation_Runs_Once_Per_Started_Batch()
    {
        // run 30 per batch of 40, 100 pieces = 3 batches
        OperationTimeCalculator.StandardMinutes(20, 30, RunTimeBasis.PerBatch, 40, 100).Should().Be(20 + 3 * 30);
        OperationTimeCalculator.StandardMinutes(20, 30, RunTimeBasis.PerBatch, 40, 80).Should().Be(20 + 2 * 30, "exactly two batches");
        OperationTimeCalculator.StandardMinutes(20, 30, RunTimeBasis.PerBatch, 40, 1).Should().Be(20 + 30);
    }

    [Theory]
    [InlineData(1000, 100, 1000)]
    [InlineData(630, 80, 788)]
    [InlineData(100, 200, 50)]
    [InlineData(100, 150, 67)]
    public void Efficiency_Stretches_The_Time_And_Is_Rounded_Up(double standard, double efficiency, int expected)
        => OperationTimeCalculator.CalendarMinutes((decimal)standard, (decimal)efficiency).Should().Be(expected);

    [Fact]
    public void Quantity_Must_Be_Positive()
        => FluentActions.Invoking(() => OperationTimeCalculator.StandardMinutes(1, 1, RunTimeBasis.PerUnit, null, 0)).Should().Throw<BusinessRuleException>();

    // ── Lead time: BR-RT-05 ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC_RT_008_Lead_Time_Of_100_Tables_Is_2003_Minutes_And_Starts_On_The_11th()
    {
        var c = new RoutingCtx();
        c.SeedItems();
        var routing = await c.SeedTopRoutingAsync();

        var lead = await c.Time.LeadTimeAsync(c.F.Routings.Single(r => r.Id == routing.Id), 100, D(2026, 11, 13));

        lead.TotalMinutes.Should().Be(1015 + 788 + 200);
        lead.TotalMinutes.Should().Be(2003);
        lead.StartDate.Should().Be(D(2026, 11, 11));
        var ops = lead.Operations.ToDictionary(o => o.OperationSeq);
        ops[10].StandardMinutes.Should().Be(1015); ops[20].StandardMinutes.Should().Be(630); ops[30].StandardMinutes.Should().Be(200);
        ops[20].CalendarMinutes.Should().Be(788);
        // op 30 on Friday 13th (200 minutes); op 20 starts Thursday 12th and ends Friday; op 10 starts Wednesday 11th and ends Thursday 12th
        ops[30].Start.Date.Should().Be(D(2026, 11, 13)); ops[30].End.Date.Should().Be(D(2026, 11, 13));
        ops[20].Start.Date.Should().Be(D(2026, 11, 12)); ops[20].End.Date.Should().Be(D(2026, 11, 13));
        ops[10].Start.Date.Should().Be(D(2026, 11, 11)); ops[10].End.Date.Should().Be(D(2026, 11, 12));
        ops[30].Start.WorkingMinute.Should().Be(700);
        ops[20].End.Should().Be(ops[30].Start, "the operations follow each other without a gap");
        ops[10].End.Should().Be(ops[20].Start);
    }

    [Fact]
    public async Task Queue_And_Move_Time_Are_Added_To_The_Lead_Time()
    {
        var c = new RoutingCtx();
        c.SeedItems();
        var r = await c.NewRoutingAsync("TP-BAN01");
        await c.AddOpAsync(r.Id, 10, "A", "WC-ASM", 0, 1, queue: 30, move: 20);
        await c.AddOpAsync(r.Id, 20, "B", "WC-PKG", 0, 1, queue: 0, move: 10);

        var lead = await c.Time.LeadTimeAsync(c.F.Routings.Single(x => x.Id == r.Id), 100, D(2026, 11, 13));

        lead.TotalMinutes.Should().Be(30 + 100 + 20 + 100 + 10);
    }

    [Fact]
    public async Task Weekend_Days_Are_Skipped_Backwards()
    {
        var c = new RoutingCtx();
        c.SeedItems();
        var r = await c.NewRoutingAsync("TP-BAN01");
        await c.AddOpAsync(r.Id, 10, "A", "WC-ASM", 0, 10); // 1000 minutes for 100 pieces: more than one day

        var lead = await c.Time.LeadTimeAsync(c.F.Routings.Single(x => x.Id == r.Id), 100, D(2026, 11, 16)); // ends Monday 16th

        lead.StartDate.Should().Be(D(2026, 11, 14), "Monday 900 + Saturday (450 working minutes) is enough; Sunday 15th has no work");
        lead.Operations.Single().Start.WorkingMinute.Should().Be(350);
    }

    [Fact]
    public async Task A_Routing_Hands_Over_Between_Calendars_Of_Different_Work_Centers()
    {
        var c = new RoutingCtx();
        c.SeedItems();
        var onlyMornings = WorkCalendar.Create(c.Plant.Id, "CAL-AM", "Mornings only");
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }) onlyMornings.SetDayPattern(day, [c.D.Ca1.ToInfo()]);
        c.F.Calendars.Add(onlyMornings);
        c.F.WorkingTime.Build = id => id == onlyMornings.Id ? new WorkingTimeCalculator(onlyMornings.ToSnapshot(c.D.Shifts)) : c.D.Calculator;
        var morning = WorkCenter.Create(c.Plant.Id, "WC-AM", Ds01.CenterValues(onlyMornings.Id, WorkCenterType.Labor));
        c.F.Of<WorkCenter>().Add(morning);
        var r = await c.NewRoutingAsync("TP-BAN01");
        await c.AddOpAsync(r.Id, RoutingCtx.Op(10, "Mornings", morning.Id, 0, 450 / 10m, queue: 0)); // 450 minutes for 10 pieces = one morning shift
        await c.AddOpAsync(r.Id, 20, "Packing", "WC-PKG", 0, 10);                                       // 100 minutes for 10 pieces

        var lead = await c.Time.LeadTimeAsync(c.F.Routings.Single(x => x.Id == r.Id), 10, D(2026, 11, 13));

        lead.TotalMinutes.Should().Be(450 + 100);
        var ops = lead.Operations.ToDictionary(o => o.OperationSeq);
        ops[10].EndTime.Should().BeOnOrBefore(ops[20].StartTime, "the morning operation finishes before packing starts");
        lead.StartDate.Should().BeOnOrBefore(D(2026, 11, 13));
    }

    [Fact]
    public void The_Latest_Working_Moment_At_Or_Before_A_Clock_Time_Is_Found()
    {
        var calc = new Ds01().Calculator;

        calc.FromDateTime(new DateTime(2026, 11, 16, 22, 0, 0)).Should().Be(new WorkPoint(D(2026, 11, 16), 900));
        calc.FromDateTime(new DateTime(2026, 11, 16, 11, 0, 0)).Should().Be(new WorkPoint(D(2026, 11, 16), 300));
        calc.FromDateTime(new DateTime(2026, 11, 15, 10, 0, 0)).Should().Be(new WorkPoint(D(2026, 11, 14), 450), "Sunday has no work: the end of Saturday");
        calc.FromDateTime(new DateTime(2026, 11, 16, 3, 0, 0)).Should().Be(new WorkPoint(D(2026, 11, 14), 450), "before Monday's first shift");
    }

    // ── BOM / routing check: BR-RT-08 ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC_BOM_021_Approving_A_BOM_With_An_Unknown_Operation_Succeeds_With_A_Warning()
    {
        var c = new RoutingCtx();
        c.SeedItems();
        await c.SeedTopRoutingAsync(); // operations 10, 20, 30
        var bom = await c.CreateAsync("TP-BAN01");
        await c.AddAsync(bom.Id, "NVL-VIT01", 16, c.Piece, op: 10);
        await c.AddAsync(bom.Id, "NVL-SON01", 0.5m, c.Litre, op: 40);

        var approved = await c.ApproveAsync(bom.Id);

        approved.Status.Should().Be(VersionStatus.Approved);
        approved.Warnings.Should().ContainSingle().Which.Should().Contain("Operation 40").And.Contain("NVL-SON01");
    }

    [Fact]
    public async Task A_BOM_Whose_Operations_All_Exist_Has_No_Warning_And_Without_A_Routing_Nothing_Is_Checked()
    {
        var c = new RoutingCtx();
        c.SeedItems();
        var plain = await c.CreateAsync("BTP-MAT01");
        await c.AddAsync(plain.Id, "NVL-GO01", 0.02m, c.M3, op: 99);
        (await c.ApproveAsync(plain.Id)).Warnings.Should().BeEmpty("BTP-MAT01 has no routing");

        await c.SeedTopRoutingAsync();
        var bom = await c.CreateAsync("TP-BAN01");
        await c.AddAsync(bom.Id, "NVL-VIT01", 16, c.Piece, op: 10);
        await c.AddAsync(bom.Id, "NVL-SON01", 0.5m, c.Litre); // a line without an operation is not checked
        (await c.ApproveAsync(bom.Id)).Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Approving_A_Routing_Warns_About_Approved_BOM_Lines_It_Does_Not_Cover()
    {
        var c = new RoutingCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01");
        await c.AddAsync(bom.Id, "NVL-VIT01", 16, c.Piece, op: 10);
        await c.AddAsync(bom.Id, "NVL-SON01", 0.5m, c.Litre, op: 20);
        await c.ApproveAsync(bom.Id);
        var routing = await c.NewRoutingAsync("TP-BAN01");
        await c.AddOpAsync(routing.Id, 10, "Assembly", "WC-ASM", 0, 1);

        var approved = await c.ApproveRoutingAsync(routing.Id);

        approved.Status.Should().Be(VersionStatus.Approved);
        approved.Warnings.Should().ContainSingle().Which.Should().Contain("Operation 20");
    }

    [Fact]
    public void An_Order_Line_With_A_Missing_Operation_Goes_To_The_First_One_With_A_Warning()
    {
        IReadOnlyCollection<int> routing = [10, 20, 30];

        BomRoutingCheck.AssignOperation(20, routing).Should().Be((20, (string?)null));
        var (seq, warning) = BomRoutingCheck.AssignOperation(40, routing);
        seq.Should().Be(10);
        warning.Should().Contain("40").And.Contain("10");
        BomRoutingCheck.AssignOperation(null, routing).Should().Be((null, (string?)null));
        BomRoutingCheck.AssignOperation(40, null).Should().Be((null, (string?)null), "no routing: the order has no operations");
    }

    // ── Used work centers ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_Work_Center_Used_By_A_Routing_Is_Reported_As_Used()
    {
        var c = new RoutingCtx();
        c.SeedItems();
        await c.SeedTopRoutingAsync();

        (await c.F.RoutingRepo.WorkCenterIsUsedAsync(c.Centers["WC-ASM"].Id)).Should().BeTrue();
        (await c.F.RoutingRepo.WorkCenterIsUsedAsync(c.Centers["WC-CUT"].Id)).Should().BeFalse();
    }
}

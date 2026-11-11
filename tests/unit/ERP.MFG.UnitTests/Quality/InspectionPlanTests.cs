using Xunit;
using ERP.MFG.Application.Features.Inspections;
using ERP.MFG.Application.Features.ItemProfiles;
using ERP.MFG.Application.Features.Routings;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.UnitTests.Boms;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;

namespace ERP.MFG.UnitTests.Quality;

/// <summary>The DS-01 items and routing on real handlers, for the tests of the inspection plans (the plan helpers are extension methods of <see cref="BomCtx"/>, see PlanHelpers).</summary>
internal class PlanCtx : RoutingCtx
{
    public static readonly DateOnly Today = new(2026, 11, 4);

    public PlanCtx() { SeedItems(); }

    public static InspectionCharacteristicBody Measure(string name, decimal? min, decimal? max, bool critical = false, int? seq = null) => PlanHelpers.Measure(name, min, max, critical, seq);

    public static InspectionCharacteristicBody Attribute(string name, bool critical = false, int? seq = null) => PlanHelpers.Attribute(name, critical, seq);
}

public class InspectionPlanTests
{
    [Fact]
    public async Task TC_QC_001_A_Measurement_With_Min_Above_Max_Is_Refused()
    {
        var c = new PlanCtx();
        var plan = await c.CreatePlanAsync("TP-BAN01", "FQC-BAN01");

        var act = () => c.AddCharAsync(plan.Id, PlanCtx.Measure("Length", 752, 748));

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Message.Should().Contain("minimum");
        c.F.InspectionPlans.Single().Characteristics.Should().BeEmpty();
    }

    [Fact]
    public async Task A_Measurement_Needs_A_Bound_And_An_Attribute_Has_None()
    {
        var c = new PlanCtx();
        var plan = await c.CreatePlanAsync("TP-BAN01", "FQC-BAN01");

        await ((Func<Task>)(() => c.AddCharAsync(plan.Id, PlanCtx.Measure("Length", null, null)))).Should().ThrowAsync<BusinessRuleException>();
        await ((Func<Task>)(() => c.AddCharAsync(plan.Id, new InspectionCharacteristicBody(null, "Colour", CharacteristicKind.Attribute, 1, 2, null, false)))).Should().ThrowAsync<BusinessRuleException>();
        await c.AddCharAsync(plan.Id, PlanCtx.Measure("Weight", 10, null));            // one-sided limits are allowed
        await c.AddCharAsync(plan.Id, PlanCtx.Measure("Width", null, 20));
    }

    [Fact]
    public async Task Characteristics_Are_Numbered_In_Steps_Of_Ten_And_The_Number_Is_Unique()
    {
        var c = new PlanCtx();
        var plan = await c.CreatePlanAsync("TP-BAN01", "FQC-BAN01");

        var first = await c.AddCharAsync(plan.Id, PlanCtx.Measure("Length", 748, 752));
        var second = await c.AddCharAsync(plan.Id, PlanCtx.Attribute("Colour"));

        first.Characteristics.Select(x => x.Seq).Should().Equal(10);
        second.Characteristics.Select(x => x.Seq).Should().Equal(10, 20);
        await ((Func<Task>)(() => c.AddCharAsync(plan.Id, PlanCtx.Attribute("Again", seq: 10)))).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task A_Plan_Without_A_Characteristic_Cannot_Be_Approved()
    {
        var c = new PlanCtx();
        var plan = await c.CreatePlanAsync("TP-BAN01", "FQC-BAN01");

        await ((Func<Task>)(() => c.ApprovePlanAsync(plan.Id))).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task An_Approved_Plan_Cannot_Be_Edited()
    {
        var c = new PlanCtx();
        var plan = await c.SeedFqcPlanAsync();

        var act = () => c.AddCharAsync(plan.Id, PlanCtx.Attribute("Extra"));

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Bom002);
    }

    [Fact]
    public async Task Approval_Records_Who_And_When()
    {
        var c = new PlanCtx();
        var plan = await c.SeedFqcPlanAsync();

        plan.Status.Should().Be(VersionStatus.Approved);
        plan.ApprovedBy.Should().NotBeNullOrEmpty();
        plan.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task The_Code_Names_The_Series_And_Cannot_Be_Used_Twice_In_A_Plant()
    {
        var c = new PlanCtx();
        await c.CreatePlanAsync("TP-BAN01", "FQC-BAN01");

        var act = () => c.CreatePlanAsync("BTP-MAT01", "FQC-BAN01");

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Gen006);
    }

    [Theory]
    [InlineData(SamplingMethod.Fixed, 0.0)]
    [InlineData(SamplingMethod.Fixed, null)]
    [InlineData(SamplingMethod.Percent, 0.0)]
    [InlineData(SamplingMethod.Percent, 100.5)]
    public async Task A_Sample_Needs_A_Sensible_Value(SamplingMethod method, double? value)
    {
        var c = new PlanCtx();

        var act = () => c.CreatePlanAsync("TP-BAN01", "FQC-BAN01", sampling: method, sample: value is null ? null : (decimal)value);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task A_Full_Inspection_Needs_No_Sample_Value_And_Drops_It()
    {
        var c = new PlanCtx();

        var plan = await c.CreatePlanAsync("TP-BAN01", "FQC-BAN01", sampling: SamplingMethod.Full, sample: 7);

        plan.SampleValue.Should().BeNull();
    }

    [Fact]
    public async Task A_Copy_Is_The_Next_Version_Of_The_Same_Series_With_All_Characteristics()
    {
        var c = new PlanCtx();
        var v1 = await c.SeedFqcPlanAsync();

        var v2 = await c.CopyPlanAsync(v1.Id, new DateOnly(2026, 12, 1));

        v2.Version.Should().Be(2);
        v2.Status.Should().Be(VersionStatus.Draft);
        (v2.Code, v2.Type, v2.ItemId).Should().Be((v1.Code, v1.Type, v1.ItemId));
        v2.Characteristics.Select(x => x.Name).Should().Equal("Length", "Colour", "Scratches", "Label");
        v2.AllowedMinorDefects.Should().Be(1);
    }

    [Fact]
    public async Task Approving_A_Later_Version_Ends_The_Open_One_The_Day_Before()
    {
        var c = new PlanCtx();
        var v1 = await c.SeedFqcPlanAsync();
        var v2 = await c.CopyPlanAsync(v1.Id, new DateOnly(2026, 12, 1));

        await c.ApprovePlanAsync(v2.Id);

        c.F.InspectionPlans.Single(p => p.Version == 1).EffectiveTo.Should().Be(new DateOnly(2026, 11, 30));
    }

    [Fact]
    public async Task A_Version_That_Starts_Before_The_Open_One_Is_An_Overlap()
    {
        var c = new PlanCtx();
        var v1 = await c.SeedFqcPlanAsync();
        var v2 = await c.CopyPlanAsync(v1.Id, new DateOnly(2025, 6, 1));

        var act = () => c.ApprovePlanAsync(v2.Id);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Bom003);
    }

    [Fact]
    public async Task Two_Plans_Of_One_Item_With_Different_Codes_Do_Not_Clash()
    {
        var c = new PlanCtx();
        await c.SeedFqcPlanAsync("FQC-A");
        var other = await c.CreatePlanAsync("TP-BAN01", "FQC-B");
        await c.AddCharAsync(other.Id, PlanCtx.Attribute("Colour"));

        var approved = await c.ApprovePlanAsync(other.Id);

        approved.Status.Should().Be(VersionStatus.Approved);
        c.F.InspectionPlans.Count(p => p.Status == VersionStatus.Approved).Should().Be(2);
    }

    [Fact]
    public async Task A_Draft_Can_Be_Deleted_And_Its_Version_Number_Is_Not_Reused()
    {
        var c = new PlanCtx();
        var v1 = await c.SeedFqcPlanAsync();
        var v2 = await c.CopyPlanAsync(v1.Id, new DateOnly(2026, 12, 1));

        await c.DeletePlanAsync(v2.Id);
        var v3 = await c.CopyPlanAsync(v1.Id, new DateOnly(2026, 12, 1));

        v3.Version.Should().Be(3);
        await ((Func<Task>)(() => c.DeletePlanAsync(v1.Id))).Should().ThrowAsync<BusinessRuleException>(); // an approved version is not deleted
    }

    [Fact]
    public async Task A_Plan_That_A_Profile_Points_At_Cannot_Be_Deleted()
    {
        var c = new PlanCtx();
        var draft = await c.SeedFqcPlanAsync(approve: false);
        c.F.Profiles.Add(ItemManufacturingProfile.Create(c.Items["TP-BAN01"].Id, c.Plant.Id, "TP-BAN01", "x", ItemManufacturingProfile.DefaultValues with { RequiresFqc = true, FqcInspectionPlanId = draft.Id }));

        var act = () => c.DeletePlanAsync(draft.Id);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Gen005);
    }

    [Fact]
    public async Task An_Obsolete_Version_Is_No_Longer_In_Force_And_A_Draft_Cannot_Be_Made_Obsolete()
    {
        var c = new PlanCtx();
        var plan = await c.SeedFqcPlanAsync();

        await c.ObsoletePlanAsync(plan.Id, new DateOnly(2026, 11, 3));

        (await c.Resolver.ResolveAsync(plan.Id, PlanCtx.Today)).Should().BeNull();
        var draft = await c.CreatePlanAsync("BTP-MAT01", "FQC-MAT01");
        await ((Func<Task>)(() => c.ObsoletePlanAsync(draft.Id))).Should().ThrowAsync<BusinessRuleException>();
    }

    // ── Resolving a plan from a pointer ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC_QC_002_A_Draft_Plan_Is_Not_Used()
    {
        var c = new PlanCtx();
        var draft = await c.SeedFqcPlanAsync(approve: false);

        (await c.Resolver.ResolveAsync(draft.Id, PlanCtx.Today)).Should().BeNull();
    }

    [Fact]
    public async Task The_Version_In_Force_On_The_Day_Is_Used_And_A_Pointer_To_An_Older_Version_Finds_It()
    {
        var c = new PlanCtx();
        var v1 = await c.SeedFqcPlanAsync();
        var v2 = await c.CopyPlanAsync(v1.Id, new DateOnly(2026, 12, 1));
        await c.AddCharAsync(v2.Id, PlanCtx.Attribute("Packaging"));
        await c.ApprovePlanAsync(v2.Id);

        (await c.Resolver.ResolveAsync(v1.Id, new DateOnly(2026, 11, 30)))!.Version.Should().Be(1);
        var later = await c.Resolver.ResolveAsync(v1.Id, new DateOnly(2026, 12, 1));
        later!.Version.Should().Be(2);
        later.Characteristics.Should().HaveCount(5);
        (await c.Resolver.ResolveAsync(null, PlanCtx.Today)).Should().BeNull();
        (await c.Resolver.ResolveAsync(Guid.NewGuid(), PlanCtx.Today)).Should().BeNull();
    }

    // ── The pointers on a profile and on a routing operation ───────────────────────────────────────────────

    [Fact]
    public async Task A_Profile_Can_Point_At_An_Fqc_Plan_Of_The_Same_Item_Only()
    {
        var c = new PlanCtx();
        var plan = await c.SeedFqcPlanAsync();
        var ipqc = await c.CreatePlanAsync("TP-BAN01", "IPQC-BAN01", InspectionType.Ipqc);
        var otherItem = await c.CreatePlanAsync("BTP-MAT01", "FQC-MAT01");
        var values = ItemManufacturingProfile.DefaultValues with { RequiresFqc = true };

        (await c.SaveProfileAsync("TP-BAN01", values with { FqcInspectionPlanId = plan.Id })).Values.FqcInspectionPlanId.Should().Be(plan.Id);
        await ((Func<Task>)(() => c.SaveProfileAsync("TP-BAN01", values with { FqcInspectionPlanId = ipqc.Id }))).Should().ThrowAsync<BusinessRuleException>();
        await ((Func<Task>)(() => c.SaveProfileAsync("TP-BAN01", values with { FqcInspectionPlanId = otherItem.Id }))).Should().ThrowAsync<BusinessRuleException>();
        await ((Func<Task>)(() => c.SaveProfileAsync("TP-BAN01", values with { FqcInspectionPlanId = Guid.NewGuid() }))).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task A_Plan_Pointer_Is_Dropped_When_The_Item_Does_Not_Require_A_Final_Inspection()
    {
        var c = new PlanCtx();
        var plan = await c.SeedFqcPlanAsync();

        var saved = await c.SaveProfileAsync("TP-BAN01", ItemManufacturingProfile.DefaultValues with { RequiresFqc = false, FqcInspectionPlanId = plan.Id });

        saved.Values.FqcInspectionPlanId.Should().BeNull();
    }

    [Fact]
    public async Task A_Routing_Operation_Can_Point_At_An_Ipqc_Plan_Of_The_Routing_Item_Only()
    {
        var c = new PlanCtx();
        var routing = await c.NewRoutingAsync("TP-BAN01");
        var ipqc = await c.CreatePlanAsync("TP-BAN01", "IPQC-BAN01", InspectionType.Ipqc);
        var fqc = await c.CreatePlanAsync("TP-BAN01", "FQC-BAN01");
        var otherItem = await c.CreatePlanAsync("BTP-MAT01", "IPQC-MAT01", InspectionType.Ipqc);

        var ok = await c.AddOpAsync(routing.Id, PlanCtx.Op(10, "Assembly", c.Centers["WC-ASM"].Id, 15, 10) with { IpqcInspectionPlanId = ipqc.Id });

        ok.Operations.Single().IpqcInspectionPlanId.Should().Be(ipqc.Id);
        await ((Func<Task>)(() => c.AddOpAsync(routing.Id, PlanCtx.Op(20, "Paint", c.Centers["WC-PNT"].Id, 30, 6) with { IpqcInspectionPlanId = fqc.Id }))).Should().ThrowAsync<BusinessRuleException>();
        await ((Func<Task>)(() => c.AddOpAsync(routing.Id, PlanCtx.Op(20, "Paint", c.Centers["WC-PNT"].Id, 30, 6) with { IpqcInspectionPlanId = otherItem.Id }))).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task A_Copy_Of_A_Routing_Keeps_The_Plan_Of_Its_Operations()
    {
        var c = new PlanCtx();
        var routing = await c.NewRoutingAsync("TP-BAN01");
        var ipqc = await c.CreatePlanAsync("TP-BAN01", "IPQC-BAN01", InspectionType.Ipqc);
        await c.AddOpAsync(routing.Id, PlanCtx.Op(10, "Assembly", c.Centers["WC-ASM"].Id, 15, 10) with { IpqcInspectionPlanId = ipqc.Id });
        await c.ApproveRoutingAsync(routing.Id);

        var copy = await c.CopyRoutingAsync(routing.Id, new DateOnly(2026, 12, 1));

        copy.Operations.Single().IpqcInspectionPlanId.Should().Be(ipqc.Id);
    }

    [Fact]
    public async Task A_Plan_Does_Not_Exist_For_A_User_Who_Cannot_See_The_Plant()
    {
        var c = new PlanCtx();
        var plan = await c.SeedFqcPlanAsync();
        var outsider = new ERP.MFG.Infrastructure.Services.PlantAccessService(c.F.PlantRepo, c.F.AccessRepo, new ERP.MFG.UnitTests.Common.FakeUser(Guid.NewGuid()));

        await FluentActions.Awaiting(() => new GetInspectionPlanHandler(outsider, c.F.PlanRepo).Handle(new GetInspectionPlanQuery(plan.Id), default))
            .Should().ThrowAsync<EntityNotFoundException>();
    }
}

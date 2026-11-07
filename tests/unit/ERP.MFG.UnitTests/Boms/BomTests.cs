using ERP.MFG.Application.Features.Boms;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Boms;

/// <summary>Bills of materials: lines, versions, approval, cycles and low-level codes on the data set DS-01 (test cases §2.6, BR-BOM-01..15).</summary>
public class BomTests
{
    private static async Task<BusinessRuleException> Refused(Func<Task> action)
        => (await FluentActions.Awaiting(action).Should().ThrowAsync<BusinessRuleException>()).Which;

    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    // ── Create, copy, edit ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC_BOM_001_A_Copy_Is_A_New_Draft_With_The_Next_Version_And_All_Lines()
    {
        var c = new BomCtx();
        var ids = await c.SeedBomsAsync();

        var copy = await c.CopyAsync(ids["TP-BAN01"], D(2026, 12, 1));

        copy.Status.Should().Be(VersionStatus.Draft);
        copy.Version.Should().Be(2);
        copy.Lines.Should().HaveCount(7);
        copy.EffectiveFrom.Should().Be(D(2026, 12, 1));
        copy.EffectiveTo.Should().BeNull();
    }

    [Fact]
    public async Task TC_BOM_002_An_Item_That_Is_Bought_Cannot_Have_A_BOM()
    {
        var c = new BomCtx();
        c.SeedItems();

        (await Refused(() => c.CreateAsync("NVL-GO01"))).RuleName.Should().Be(MfgErrors.Bom001);
    }

    [Fact]
    public async Task An_Item_With_No_Profile_Counts_As_Made()
    {
        var c = new BomCtx();
        c.Item("NEW-ITEM", c.Piece);

        (await c.CreateAsync("NEW-ITEM")).Version.Should().Be(1);
    }

    [Fact]
    public async Task TC_BOM_003_A_Component_That_Is_Not_Active_Is_Refused()
    {
        var c = new BomCtx();
        c.SeedItems();
        c.Item("OLD-PART", c.Piece, active: false);
        var bom = await c.CreateAsync("TP-BAN01");

        (await Refused(() => c.AddAsync(bom.Id, "OLD-PART", 1, c.Piece))).RuleName.Should().Be(MfgErrors.Bom001);
    }

    [Fact]
    public async Task TC_BOM_004_An_Approved_BOM_Cannot_Be_Edited()
    {
        var c = new BomCtx();
        var ids = await c.SeedBomsAsync();

        (await Refused(() => c.AddAsync(ids["TP-BAN01"], "NVL-VIT01", 1, c.Piece, op: 99))).RuleName.Should().Be(MfgErrors.Bom002);
        (await Refused(() => new UpdateBomLineHandler(c.Access, c.F.BomRepo, c.Snapshots, c.F.Uow)
            .Handle(new UpdateBomLineCommand(ids["TP-BAN01"], 30, BomCtx.Line(c.Items["NVL-VIT01"], 17, c.Piece, op: 10)), default))).RuleName.Should().Be(MfgErrors.Bom002);
        (await Refused(() => new DeleteBomHandler(c.Access, c.F.BomRepo, c.F.Uow).Handle(new DeleteBomCommand(ids["TP-BAN01"]), default))).RuleName.Should().Be(MfgErrors.Bom002);
    }

    [Fact]
    public async Task A_Draft_Can_Be_Edited_And_Deleted_And_Its_Version_Number_Is_Not_Reused()
    {
        var c = new BomCtx();
        c.SeedItems();
        var draft = await c.CreateAsync("TP-BAN01");
        await c.AddAsync(draft.Id, "NVL-VIT01", 16, c.Piece, op: 10);

        var changed = await new UpdateBomLineHandler(c.Access, c.F.BomRepo, c.Snapshots, c.F.Uow)
            .Handle(new UpdateBomLineCommand(draft.Id, 10, BomCtx.Line(c.Items["NVL-VIT01"], 18, c.Piece, op: 10)), default);
        changed.Lines.Single().Quantity.Should().Be(18);

        await new DeleteBomHandler(c.Access, c.F.BomRepo, c.F.Uow).Handle(new DeleteBomCommand(draft.Id), default);
        (await c.CreateAsync("TP-BAN01")).Version.Should().Be(2);
    }

    [Fact]
    public async Task TC_BOM_005_Approval_Records_Who_And_When()
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01");
        await c.AddAsync(bom.Id, "NVL-VIT01", 16, c.Piece);

        var approved = await c.ApproveAsync(bom.Id);

        approved.Status.Should().Be(VersionStatus.Approved);
        approved.ApprovedBy.Should().NotBeNullOrEmpty();
        approved.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_BOM_With_No_Component_Cannot_Be_Approved()
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01");

        await FluentActions.Awaiting(() => c.ApproveAsync(bom.Id)).Should().ThrowAsync<BusinessRuleException>();
    }

    // ── Versions and validity (BR-BOM-03) ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC_BOM_006_Approving_A_Later_Version_Ends_The_Open_One_The_Day_Before()
    {
        var c = new BomCtx();
        var ids = await c.SeedBomsAsync();
        var v2 = await c.CopyAsync(ids["TP-BAN01"], D(2026, 12, 1));

        await c.ApproveAsync(v2.Id);

        c.F.Boms.Single(b => b.Id == ids["TP-BAN01"]).EffectiveTo.Should().Be(D(2026, 11, 30));
        c.F.Boms.Single(b => b.Id == v2.Id).Status.Should().Be(VersionStatus.Approved);
    }

    [Fact]
    public async Task TC_BOM_007_A_Version_That_Overlaps_A_Closed_Period_Is_Refused_With_The_Clashing_Version()
    {
        var c = new BomCtx();
        c.SeedItems();
        var v1 = await c.CreateAsync("TP-BAN01", D(2026, 1, 1), D(2026, 12, 31));
        await c.AddAsync(v1.Id, "NVL-VIT01", 16, c.Piece);
        await c.ApproveAsync(v1.Id);
        var v2 = await c.CopyAsync(v1.Id, D(2026, 6, 1));

        var refusal = await Refused(() => c.ApproveAsync(v2.Id));

        refusal.RuleName.Should().Be(MfgErrors.Bom003);
        refusal.Message.Should().Contain("1");
        c.F.Boms.Single(b => b.Id == v2.Id).Status.Should().Be(VersionStatus.Draft);
        c.F.Boms.Single(b => b.Id == v1.Id).EffectiveTo.Should().Be(D(2026, 12, 31));
    }

    [Fact]
    public async Task A_Version_That_Starts_Before_The_Open_One_Is_An_Overlap()
    {
        var c = new BomCtx();
        var ids = await c.SeedBomsAsync();
        var earlier = await c.CopyAsync(ids["TP-BAN01"], D(2025, 6, 1)); // open-ended, starts before v1 (2026-01-01)

        (await Refused(() => c.ApproveAsync(earlier.Id))).RuleName.Should().Be(MfgErrors.Bom003);
    }

    [Fact]
    public async Task TC_BOM_008_The_Version_That_Applies_Depends_On_The_Date()
    {
        var c = new BomCtx();
        var ids = await c.SeedBomsAsync();
        var v2 = await c.CopyAsync(ids["TP-BAN01"], D(2026, 12, 1));
        await c.ApproveAsync(v2.Id);
        var item = c.Items["TP-BAN01"].Id;
        var resolver = new ERP.MFG.Application.Services.BomResolver(c.F.BomRepo);

        (await resolver.GetEffectiveBomAsync(item, c.Plant.Id, D(2026, 11, 30)))!.Version.Should().Be(1);
        (await resolver.GetEffectiveBomAsync(item, c.Plant.Id, D(2026, 12, 1)))!.Version.Should().Be(2);
        (await resolver.GetEffectiveBomAsync(item, c.Plant.Id, D(2025, 12, 31))).Should().BeNull();
    }

    [Fact]
    public async Task TC_BOM_020_An_Obsolete_Version_Is_No_Longer_Effective_And_Only_Its_End_Can_Change()
    {
        var c = new BomCtx();
        var ids = await c.SeedBomsAsync();

        var obsolete = await c.ObsoleteAsync(ids["TP-BAN01"], D(2026, 11, 10));

        obsolete.Status.Should().Be(VersionStatus.Obsolete);
        obsolete.EffectiveTo.Should().Be(D(2026, 11, 10));
        (await new ERP.MFG.Application.Services.BomResolver(c.F.BomRepo).GetEffectiveBomAsync(c.Items["TP-BAN01"].Id, c.Plant.Id, D(2026, 11, 5))).Should().BeNull();
        (await Refused(() => c.AddAsync(ids["TP-BAN01"], "NVL-VIT01", 1, c.Piece, op: 99))).RuleName.Should().Be(MfgErrors.Bom002);

        var moved = await new ObsoleteBomHandler(c.Access, c.F.BomRepo, c.F.ProfileRepo, c.F.Uow).Handle(new ObsoleteBomCommand(ids["TP-BAN01"], D(2026, 11, 8)), default);
        moved.EffectiveTo.Should().Be(D(2026, 11, 8));
    }

    [Fact]
    public async Task A_Draft_Cannot_Be_Made_Obsolete()
    {
        var c = new BomCtx();
        c.SeedItems();
        var draft = await c.CreateAsync("TP-BAN01");

        (await Refused(() => c.ObsoleteAsync(draft.Id))).RuleName.Should().Be(MfgErrors.Bom002);
    }

    // ── Cycles (BR-BOM-04) ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC_BOM_009_An_Item_Cannot_Be_Its_Own_Component()
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01");

        var refusal = await Refused(() => c.AddAsync(bom.Id, "TP-BAN01", 1, c.Piece));

        refusal.RuleName.Should().Be(MfgErrors.Bom004);
        refusal.Message.Should().Contain("TP-BAN01 > TP-BAN01");
    }

    [Fact]
    public async Task TC_BOM_010_A_Cycle_Over_Several_Levels_Is_Refused_At_Approval_With_Its_Path()
    {
        var c = new BomCtx();
        var ids = await c.SeedBomsAsync();
        var v2 = await c.CopyAsync(ids["BTP-MAT01"], D(2026, 12, 1)); // BTP-MAT01 v2 will also contain TP-BAN01, which contains BTP-MAT01
        await c.AddAsync(v2.Id, "TP-BAN01", 1, c.Piece, op: 20);

        var refusal = await Refused(() => c.ApproveAsync(v2.Id));

        refusal.RuleName.Should().Be(MfgErrors.Bom004);
        refusal.Message.Should().Contain("BTP-MAT01 > TP-BAN01 > BTP-MAT01");
        c.F.Boms.Single(b => b.Id == v2.Id).Status.Should().Be(VersionStatus.Draft);
        c.F.Boms.Single(b => b.Id == ids["BTP-MAT01"]).EffectiveTo.Should().BeNull("a refused approval changes nothing");
    }

    [Fact]
    public void The_Cycle_Finder_Handles_Long_Cycles_And_Diamonds_That_Are_Not_Cycles()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), x = Guid.NewGuid(), y = Guid.NewGuid();
        var diamond = new Dictionary<Guid, HashSet<Guid>> { [a] = [b, x], [b] = [y], [x] = [y] };
        BomGraph.FindCycle(diamond, a).Should().BeNull();

        var loop = new Dictionary<Guid, HashSet<Guid>> { [a] = [b], [b] = [x], [x] = [a] };
        var cycle = BomGraph.FindCycle(loop, a)!;
        cycle.Should().Equal(a, b, x, a);
    }

    // ── Numbers and rules of one line (BR-BOM-05, -07, -09, -11, -12) ───────────────────────────────────

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.1234567")]
    public async Task TC_BOM_011_A_Quantity_That_Is_Not_Positive_Or_Has_Too_Many_Decimals_Is_Refused(string quantity)
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01");

        (await Refused(() => c.AddAsync(bom.Id, "NVL-VIT01", decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture), c.Piece))).RuleName.Should().Be(MfgErrors.Bom005);
    }

    [Theory]
    [InlineData(99.99, true)]
    [InlineData(100, false)]
    [InlineData(-0.01, false)]
    public async Task TC_BOM_012_Scrap_Is_Zero_To_99_99(double scrap, bool accepted)
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01");
        Func<Task> add = () => c.AddAsync(bom.Id, "NVL-VIT01", 1, c.Piece, scrap: (decimal)scrap);

        if (accepted) await add.Should().NotThrowAsync();
        else (await Refused(add)).RuleName.Should().Be(MfgErrors.Bom005);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(80.5, true)]
    [InlineData(0, false)]
    [InlineData(100.01, false)]
    public async Task TC_BOM_012_Yield_Is_Above_0_And_At_Most_100(double yield, bool accepted)
    {
        var c = new BomCtx();
        c.SeedItems();
        Func<Task> create = () => c.CreateAsync("TP-BAN01", yield: (decimal)yield);

        if (accepted) await create.Should().NotThrowAsync();
        else (await Refused(create)).RuleName.Should().Be(MfgErrors.Bom005);
    }

    [Fact]
    public async Task TC_BOM_013_A_Unit_That_Cannot_Be_Converted_To_The_Components_Base_Unit_Is_Refused()
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01");

        var refusal = await Refused(() => c.AddAsync(bom.Id, "NVL-SON01", 0.5m, c.Piece)); // paint counted in pieces

        refusal.RuleName.Should().Be(MfgErrors.Bom006);
        refusal.Message.Should().Contain("NVL-SON01");
    }

    [Fact]
    public async Task A_Unit_Of_The_Same_Family_Converts_Through_The_Base_Unit()
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01");

        await c.AddAsync(bom.Id, "NVL-KEO01", 500, c.Gram); // 500 g of glue that counts in kg

        var order = await c.Explosion.ExplodeAsync(c.Plant.Id, c.Items["TP-BAN01"].Id, bom.Id, 2, BomCtx.Nov4);
        order.Requirements.Single().Quantity.Should().Be(1.00m); // 2 x 0.5 kg
    }

    [Fact]
    public async Task A_Conversion_Of_The_Product_Is_Used_When_Units_Are_Not_Of_One_Family()
    {
        var c = new BomCtx();
        c.SeedItems();
        c.F.ProductConversions.Add(new ProductConversionFacts(c.Items["NVL-SON01"].Id, c.Piece, c.Litre, 2.5m)); // 1 can = 2.5 litres
        var bom = await c.CreateAsync("TP-BAN01");

        await c.AddAsync(bom.Id, "NVL-SON01", 1, c.Piece);

        var order = await c.Explosion.ExplodeAsync(c.Plant.Id, c.Items["TP-BAN01"].Id, bom.Id, 2, BomCtx.Nov4);
        order.Requirements.Single().Quantity.Should().Be(5.00m);
    }

    [Fact]
    public async Task TC_INT_001_One_Box_Of_100_Pieces_Gives_200_Pieces_For_An_Order_Of_2()
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01");
        await c.AddAsync(bom.Id, "NVL-VIT01", 1, c.Box);

        var order = await c.Explosion.ExplodeAsync(c.Plant.Id, c.Items["TP-BAN01"].Id, bom.Id, 2, BomCtx.Nov4);

        order.Requirements.Single().Quantity.Should().Be(200m);
    }

    [Fact]
    public async Task TC_BOM_014_Co_Product_Shares_Of_The_Cost_Must_Stay_Below_100_Percent()
    {
        var c = new BomCtx();
        c.SeedItems();
        c.Item("CO-A", c.Piece); c.Item("CO-B", c.Piece);
        var bom = await c.CreateAsync("TP-BAN01");
        await c.AddAsync(bom.Id, "CO-A", 1, c.Piece, type: BomLineType.CoProduct, allocation: 60);

        (await Refused(() => c.AddAsync(bom.Id, "CO-B", 1, c.Piece, type: BomLineType.CoProduct, allocation: 40))).RuleName.Should().Be(MfgErrors.Bom007);
        await c.AddAsync(bom.Id, "CO-B", 1, c.Piece, type: BomLineType.CoProduct, allocation: 39.99m);
    }

    [Fact]
    public async Task TC_BOM_015_An_Alternative_Group_Without_A_First_Priority_Cannot_Be_Approved()
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01");
        await c.AddAsync(bom.Id, "NVL-KEO01", 0.1m, c.Kg, group: "G2", priority: 2);
        await c.AddAsync(bom.Id, "NVL-KEO02", 0.12m, c.Kg, group: "G2", priority: 3);

        (await Refused(() => c.ApproveAsync(bom.Id))).RuleName.Should().Be(MfgErrors.Bom009);
    }

    [Fact]
    public async Task TC_BOM_016_An_Alternative_Group_With_Two_First_Priorities_Cannot_Be_Approved()
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01");
        await c.AddAsync(bom.Id, "NVL-KEO01", 0.1m, c.Kg, group: "G2", priority: 1);
        await c.AddAsync(bom.Id, "NVL-KEO02", 0.12m, c.Kg, group: "G2", priority: 1);

        var refusal = await Refused(() => c.ApproveAsync(bom.Id));
        refusal.RuleName.Should().Be(MfgErrors.Bom009);
        refusal.Message.Should().Contain("G2");
    }

    [Fact]
    public async Task A_Group_Needs_A_Priority_And_A_Priority_Needs_A_Group()
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01");

        await FluentActions.Awaiting(() => c.AddAsync(bom.Id, "NVL-KEO01", 1, c.Kg, group: "G1")).Should().ThrowAsync<BusinessRuleException>();
        await FluentActions.Awaiting(() => c.AddAsync(bom.Id, "NVL-KEO01", 1, c.Kg, priority: 1)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task TC_BOM_017_The_Same_Component_Twice_At_The_Same_Operation_Is_Refused()
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01");
        await c.AddAsync(bom.Id, "NVL-VIT01", 16, c.Piece, op: 10);

        var refusal = await Refused(() => c.AddAsync(bom.Id, "NVL-VIT01", 4, c.Piece, op: 10));

        refusal.RuleName.Should().Be(MfgErrors.Bom010);
        refusal.Message.Should().Contain("NVL-VIT01").And.Contain("10");
    }

    [Fact]
    public async Task TC_BOM_018_The_Same_Component_At_Another_Operation_Is_Allowed()
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01");
        await c.AddAsync(bom.Id, "NVL-VIT01", 16, c.Piece, op: 10);

        await FluentActions.Awaiting(() => c.AddAsync(bom.Id, "NVL-VIT01", 4, c.Piece, op: 30)).Should().NotThrowAsync();
    }

    // ── Low-level codes (BR-BOM-13) ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC_BOM_019_Low_Level_Codes_Of_DS01_Are_Written_To_The_Profiles()
    {
        var c = new BomCtx();
        await c.SeedBomsAsync();

        int Llc(string code) => c.F.Profiles.Single(p => p.ItemCode == code).LowLevelCode;

        Llc("TP-BAN01").Should().Be(0);
        foreach (var one in new[] { "BTP-MAT01", "BTP-CHAN01", "NVL-VIT01", "NVL-SON01", "NVL-KEO01", "NVL-KEO02", "KIT-PKG01" }) Llc(one).Should().Be(1, one);
        foreach (var two in new[] { "NVL-GO01", "NVL-THUNG01", "NVL-XOP01" }) Llc(two).Should().Be(2, two);
    }

    [Fact]
    public async Task Making_A_BOM_Obsolete_Recomputes_The_Low_Level_Codes()
    {
        var c = new BomCtx();
        var ids = await c.SeedBomsAsync();

        await c.ObsoleteAsync(ids["TP-BAN01"], D(2026, 11, 4));

        c.F.Profiles.Single(p => p.ItemCode == "BTP-MAT01").LowLevelCode.Should().Be(0, "nothing consumes it any more");
        c.F.Profiles.Single(p => p.ItemCode == "NVL-GO01").LowLevelCode.Should().Be(1);
        c.F.Profiles.Single(p => p.ItemCode == "NVL-VIT01").LowLevelCode.Should().Be(0, "no approved BOM lists it any more");
    }

    [Fact]
    public async Task The_Low_Level_Codes_Query_Includes_Items_Without_A_Profile()
    {
        var c = new BomCtx();
        await c.SeedBomsAsync();
        c.F.Profiles.RemoveAll(p => p.ItemCode == "NVL-GO01");

        var codes = await new GetLowLevelCodesHandler(c.Access, c.F.BomRepo).Handle(new GetLowLevelCodesQuery(c.Plant.Id), default);

        codes.Single(x => x.ItemCode == "NVL-GO01").LowLevelCode.Should().Be(2);
        codes.Single(x => x.ItemCode == "TP-BAN01").LowLevelCode.Should().Be(0);
    }

    [Fact]
    public async Task A_BOM_Deeper_Than_MaxBomLevels_Cannot_Be_Approved()
    {
        var c = new BomCtx { Settings = new FixedSettings(1) };
        c.SeedItems();
        var mat = await c.CreateAsync("BTP-MAT01");
        await c.AddAsync(mat.Id, "NVL-GO01", 0.02m, c.M3);
        await c.ApproveAsync(mat.Id); // one level: fine
        var top = await c.CreateAsync("TP-BAN01");
        await c.AddAsync(top.Id, "BTP-MAT01", 1, c.Piece);

        (await Refused(() => c.ApproveAsync(top.Id))).RuleName.Should().Be(MfgErrors.Bom008);
    }

    // ── Reading and reports ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC_BOM_050_Where_Used_Lists_Parents_By_Level()
    {
        var c = new BomCtx();
        await c.SeedBomsAsync();

        var rows = await new WhereUsedHandler(c.Access, c.F.BomRepo, c.Settings).Handle(new WhereUsedQuery(c.Plant.Id, c.Items["NVL-GO01"].Id), default);

        rows.Where(r => r.Level == 1).Select(r => r.ParentCode).Should().BeEquivalentTo("BTP-MAT01", "BTP-CHAN01");
        rows.Where(r => r.Level == 2).Select(r => r.ParentCode).Should().BeEquivalentTo("TP-BAN01");
    }

    [Fact]
    public async Task TC_BOM_051_Compare_Shows_One_Changed_Quantity_And_One_Added_Line()
    {
        var c = new BomCtx();
        var ids = await c.SeedBomsAsync();
        var v2 = await c.CopyAsync(ids["TP-BAN01"], D(2026, 12, 1));
        await new UpdateBomLineHandler(c.Access, c.F.BomRepo, c.Snapshots, c.F.Uow)
            .Handle(new UpdateBomLineCommand(v2.Id, 30, BomCtx.Line(c.Items["NVL-VIT01"], 18, c.Piece, op: 10, issue: IssueMethod.Backflush)), default);
        c.Item("NVL-KEO03", c.Kg);
        await c.AddAsync(v2.Id, "NVL-KEO03", 0.05m, c.Kg, op: 10);

        var diff = await new CompareBomsHandler(c.Access, c.F.BomRepo).Handle(new CompareBomsQuery(ids["TP-BAN01"], v2.Id), default);

        diff.Should().HaveCount(2);
        diff.Single(d => d.Kind == "Changed").Should().Match<BomDifference>(d => d.ComponentCode == "NVL-VIT01" && d.LeftQuantity == 16 && d.RightQuantity == 18);
        diff.Single(d => d.Kind == "Added").ComponentCode.Should().Be("NVL-KEO03");
    }

    [Fact]
    public async Task A_BOM_Of_Another_Plant_Does_Not_Exist_For_A_User_Who_Cannot_See_The_Plant()
    {
        var c = new BomCtx();
        var ids = await c.SeedBomsAsync();
        var outsider = new ERP.MFG.Infrastructure.Services.PlantAccessService(c.F.PlantRepo, c.F.AccessRepo, new ERP.MFG.UnitTests.Common.FakeUser(Guid.NewGuid()));

        await FluentActions.Awaiting(() => new GetBomHandler(outsider, c.F.BomRepo).Handle(new GetBomQuery(ids["TP-BAN01"]), default))
            .Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task A_Service_That_Cannot_Be_Reached_Refuses_The_Line_It_Never_Answers_Empty()
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01");
        c.F.Unreachable = true;

        (await Refused(() => c.AddAsync(bom.Id, "NVL-VIT01", 1, c.Piece))).RuleName.Should().Be(MfgErrors.Int001);
    }
}

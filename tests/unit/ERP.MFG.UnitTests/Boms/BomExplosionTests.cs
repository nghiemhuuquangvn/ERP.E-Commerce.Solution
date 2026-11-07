using System.Diagnostics;
using ERP.MFG.Application.Features.Boms;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Boms;

/// <summary>The material requirement arithmetic on DS-01 (test cases §1.9 and TC-BOM-030..040, BR-BOM-06, -08, -10, -11).</summary>
public class BomExplosionTests
{
    private static async Task<(BomCtx Ctx, Dictionary<string, Guid> Ids)> Seeded(int maxLevels = 20)
    {
        var c = new BomCtx { Settings = new FixedSettings(maxLevels) };
        var ids = await c.SeedBomsAsync();
        return (c, ids);
    }

    private static Task<ExplosionResult> Explode(BomCtx c, string item, decimal quantity, Guid? bomId = null)
        => c.Explosion.ExplodeAsync(c.Plant.Id, c.Items[item].Id, bomId, quantity, BomCtx.Nov4);

    private static decimal Need(ExplosionResult r, string code) => r.Requirements.Single(x => x.ComponentCode == code).Quantity;

    [Fact]
    public async Task TC_BOM_030_The_Standard_Order_Of_100_Needs_The_Seven_Lines_Of_DS01()
    {
        var (c, _) = await Seeded();

        var result = await Explode(c, "TP-BAN01", 100);

        result.Requirements.Should().HaveCount(7);
        Need(result, "BTP-MAT01").Should().Be(100m);
        Need(result, "BTP-CHAN01").Should().Be(400m);
        Need(result, "NVL-VIT01").Should().Be(1600m);
        Need(result, "NVL-SON01").Should().Be(52.50m);
        Need(result, "NVL-KEO01").Should().Be(10.00m);
        Need(result, "NVL-THUNG01").Should().Be(100m);
        Need(result, "NVL-XOP01").Should().Be(200m);
        result.Requirements.Single(x => x.ComponentCode == "NVL-VIT01").IssueMethod.Should().Be(IssueMethod.Backflush);
        result.Version.Should().Be(1);
    }

    [Fact]
    public async Task TC_BOM_031_Quantities_Are_Rounded_Up_To_The_Decimals_Of_The_Components_Unit()
    {
        var (c, _) = await Seeded();

        var result = await Explode(c, "TP-BAN01", 7);

        Need(result, "NVL-SON01").Should().Be(3.68m, "7 x 0.5 x 1.05 = 3.675, litres have 2 decimals");
        Need(result, "NVL-KEO01").Should().Be(0.70m);
        Need(result, "BTP-CHAN01").Should().Be(28m);
    }

    [Fact]
    public async Task TC_BOM_032_A_Phantom_Is_Replaced_By_Its_Components_At_The_Phantoms_Operation()
    {
        var (c, _) = await Seeded();

        var result = await Explode(c, "TP-BAN01", 100);

        result.Requirements.Should().NotContain(x => x.ComponentCode == "KIT-PKG01");
        result.Requirements.Single(x => x.ComponentCode == "NVL-THUNG01").Should().Match<RequirementLine>(x => x.Quantity == 100 && x.OperationSeq == 30);
        result.Requirements.Single(x => x.ComponentCode == "NVL-XOP01").Should().Match<RequirementLine>(x => x.Quantity == 200 && x.OperationSeq == 30);
    }

    [Fact]
    public async Task TC_BOM_033_A_Phantom_Without_An_Effective_BOM_Is_Refused_By_Name()
    {
        var (c, ids) = await Seeded();
        await c.ObsoleteAsync(ids["KIT-PKG01"], new DateOnly(2026, 11, 1));

        var refusal = await FluentActions.Awaiting(() => Explode(c, "TP-BAN01", 100)).Should().ThrowAsync<BusinessRuleException>();

        refusal.Which.RuleName.Should().Be(MfgErrors.Bom011);
        refusal.Which.Message.Should().Contain("KIT-PKG01");
    }

    [Fact]
    public async Task TC_BOM_034_Only_The_First_Priority_Of_An_Alternative_Group_Is_Counted()
    {
        var (c, _) = await Seeded();

        var result = await Explode(c, "TP-BAN01", 100);

        Need(result, "NVL-KEO01").Should().Be(10.00m);
        result.Requirements.Should().NotContain(x => x.ComponentCode == "NVL-KEO02");
    }

    [Fact]
    public async Task TC_BOM_035_A_Fixed_Quantity_Does_Not_Depend_On_The_Order_And_Takes_No_Yield()
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01", yield: 80);
        await c.AddAsync(bom.Id, "NVL-SON01", 0.2m, c.Litre, fixedQty: true);

        (await Explode(c, "TP-BAN01", 1, bom.Id)).Requirements.Single().Quantity.Should().Be(0.20m);
        (await Explode(c, "TP-BAN01", 500, bom.Id)).Requirements.Single().Quantity.Should().Be(0.20m);
    }

    [Fact]
    public async Task TC_BOM_036_A_Base_Quantity_Other_Than_One_Scales_The_Lines()
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01", baseQty: 10);
        await c.AddAsync(bom.Id, "NVL-VIT01", 160, c.Piece);

        (await Explode(c, "TP-BAN01", 100, bom.Id)).Requirements.Single().Quantity.Should().Be(1600m);
    }

    [Fact]
    public async Task TC_BOM_037_A_Yield_Below_100_Raises_The_Need()
    {
        var c = new BomCtx();
        c.SeedItems();
        var bom = await c.CreateAsync("TP-BAN01", yield: 80);
        await c.AddAsync(bom.Id, "NVL-VIT01", 16, c.Piece);

        (await Explode(c, "TP-BAN01", 100, bom.Id)).Requirements.Single().Quantity.Should().Be(2000m);
    }

    [Fact]
    public async Task TC_BOM_038_Wood_Is_Needed_With_Scrap_And_The_By_Product_Is_Planned_Without_It()
    {
        var (c, _) = await Seeded();

        var result = await Explode(c, "BTP-MAT01", 50);

        Need(result, "NVL-GO01").Should().Be(1.100m, "50 x 0.02 x 1.10");
        result.Outputs.Should().ContainSingle().Which.Should().Match<OutputLine>(o => o.ItemCode == "PP-MUN01" && o.PlannedQuantity == 25.00m && o.LineType == BomLineType.ByProduct);
    }

    [Fact]
    public async Task TC_BOM_039_The_Indented_Tree_Has_Three_Levels_And_Wood_Under_Both_Parents()
    {
        var (c, _) = await Seeded();

        var tree = await c.Explosion.IndentedAsync(c.Plant.Id, c.Items["TP-BAN01"].Id, null, 1, BomCtx.Nov4);

        var mat = tree.Children.Single(n => n.ItemCode == "BTP-MAT01");
        var chan = tree.Children.Single(n => n.ItemCode == "BTP-CHAN01");
        mat.Children.Single(n => n.ItemCode == "NVL-GO01").Quantity.Should().Be(0.022m);
        chan.Quantity.Should().Be(4m);
        chan.Children.Single(n => n.ItemCode == "NVL-GO01").Quantity.Should().Be(0.022m, "4 x 0.005 x 1.10");
        tree.Children.Single(n => n.ItemCode == "KIT-PKG01").Should().Match<IndentedNode>(n => n.IsPhantom && n.Children.Count == 2);
        tree.Children.Single(n => n.ItemCode == "NVL-KEO02").Should().Match<IndentedNode>(n => n.IsSubstitute && n.AlternativeGroup == "G1");
        tree.Children.Single(n => n.ItemCode == "NVL-VIT01").Children.Should().BeEmpty("a bought item is a leaf");
    }

    [Fact]
    public async Task TC_BOM_040_A_Tree_Deeper_Than_MaxBomLevels_Is_Refused_By_Name()
    {
        var (c, _) = await Seeded();
        c.Settings = new FixedSettings(1);

        var refusal = await FluentActions.Awaiting(() => c.Explosion.IndentedAsync(c.Plant.Id, c.Items["TP-BAN01"].Id, null, 1, BomCtx.Nov4)).Should().ThrowAsync<BusinessRuleException>();

        refusal.Which.RuleName.Should().Be(MfgErrors.Bom008);
    }

    [Fact]
    public async Task An_Order_Date_Without_An_Effective_BOM_Is_Refused()
    {
        var (c, _) = await Seeded();

        var refusal = await FluentActions.Awaiting(() => c.Explosion.ExplodeAsync(c.Plant.Id, c.Items["TP-BAN01"].Id, null, 10, new DateOnly(2025, 6, 1))).Should().ThrowAsync<BusinessRuleException>();

        refusal.Which.RuleName.Should().Be(MfgErrors.Po004);
    }

    [Fact]
    public async Task A_Quantity_Of_Zero_Is_Refused()
    {
        var (c, _) = await Seeded();

        (await FluentActions.Awaiting(() => Explode(c, "TP-BAN01", 0)).Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po001);
    }

    [Fact]
    public async Task A_Draft_Can_Be_Exploded_By_Its_Id_Before_It_Is_Approved()
    {
        var c = new BomCtx();
        c.SeedItems();
        var draft = await c.CreateAsync("TP-BAN01");
        await c.AddAsync(draft.Id, "NVL-VIT01", 16, c.Piece);

        (await Explode(c, "TP-BAN01", 10, draft.Id)).Requirements.Single().Quantity.Should().Be(160m);
    }

    [Fact]
    public async Task The_Same_Component_From_The_Bom_And_A_Phantom_At_One_Operation_Is_One_Line()
    {
        var c = new BomCtx();
        c.SeedItems();
        var kit = await c.CreateAsync("KIT-PKG01");
        await c.AddAsync(kit.Id, "NVL-THUNG01", 1, c.Piece);
        await c.ApproveAsync(kit.Id);
        var top = await c.CreateAsync("TP-BAN01");
        await c.AddAsync(top.Id, "KIT-PKG01", 1, c.Set, op: 30, phantom: true);
        await c.AddAsync(top.Id, "NVL-THUNG01", 1, c.Piece, op: 30);

        var result = await Explode(c, "TP-BAN01", 10, top.Id);

        result.Requirements.Should().ContainSingle().Which.Quantity.Should().Be(20m);
    }

    [Fact]
    public void Rounding_Up_Is_Exact_For_Values_That_Are_Exact_On_Paper()
    {
        BomExplosionCalculator.RoundUp(3.675m, 2).Should().Be(3.68m);
        BomExplosionCalculator.RoundUp(3.68m, 2).Should().Be(3.68m);
        BomExplosionCalculator.RoundUp(0.0000001m, 3).Should().Be(0.001m);
        BomExplosionCalculator.RoundUp(7m, 0).Should().Be(7m);
    }

    [Fact]
    public async Task NFR_02_A_Five_Level_Tree_Of_200_Lines_Per_Level_Explodes_Within_A_Second()
    {
        var c = new BomCtx();
        c.SeedItems();
        // five levels; each BOM has 200 lines: 199 bought parts and the made item of the next level
        var made = Enumerable.Range(1, 5).Select(i => c.Item($"LVL{i}", c.Piece)).ToList();
        var bought = Enumerable.Range(1, 199).Select(i => c.Item($"PART{i:000}", c.Piece)).ToList();

        for (var level = 4; level >= 0; level--)
        {
            var bom = await c.CreateAsync($"LVL{level + 1}");
            if (level < 4) await c.AddAsync(bom.Id, $"LVL{level + 2}", 2, c.Piece);
            foreach (var part in bought) await c.AddAsync(bom.Id, part.Sku, 1, c.Piece);
            await c.ApproveAsync(bom.Id);
        }

        var watch = Stopwatch.StartNew();
        var tree = await c.Explosion.IndentedAsync(c.Plant.Id, made[0].Id, null, 1, BomCtx.Nov4);
        watch.Stop();

        tree.Children.Count.Should().Be(200);
        watch.ElapsedMilliseconds.Should().BeLessThan(1000);
    }
}

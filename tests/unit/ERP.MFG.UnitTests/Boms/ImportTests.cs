using ERP.MFG.Application.Features.Imports;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Boms;

/// <summary>CSV import of a BOM and of a routing: all-or-nothing, every problem with its line number (T2.9, TC-BOM-053).</summary>
public class ImportTests
{
    private const string BomHeader = "componentSku,quantity,uom,scrapPct,operationSeq,issueMethod,lineType,isPhantom,isFixedQuantity,alternativeGroup,alternativePriority,costAllocationPct,notes";
    private const string RoutingHeader = "operationSeq,name,workCenter,standardOperation,setupMinutes,runMinutes,basis,batchSize,queueMinutes,moveMinutes,isSubcontract,instructions";

    private static Task<ImportResult> ImportBom(RoutingCtx c, bool dryRun, params string[] lines)
        => c.BomImport.Handle(new ImportBomCommand(c.Plant.Id, c.Items["TP-BAN01"].Id, null, null, null, dryRun, RoutingCtx.Csv(lines)), default);

    private static Task<ImportResult> ImportRouting(RoutingCtx c, bool dryRun, params string[] lines)
        => c.RoutingImport.Handle(new ImportRoutingCommand(c.Plant.Id, c.Items["TP-BAN01"].Id, null, dryRun, RoutingCtx.Csv(lines)), default);

    private static RoutingCtx Seeded()
    {
        var c = new RoutingCtx();
        c.SeedItems();
        return c;
    }

    // ── BOM ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_Clean_BOM_File_Creates_A_Draft_With_All_Its_Lines()
    {
        var c = Seeded();

        var result = await ImportBom(c, false, BomHeader,
            "NVL-VIT01,16,PCS,0,10,Backflush,Component,false,false,,,,",
            "NVL-SON01,0.5,L,5,20,Manual,Component,false,false,,,,Two coats",
            "NVL-KEO01,0.1,KG,0,10,Manual,Component,false,false,g1,1,,",
            "NVL-KEO02,0.12,KG,0,10,Manual,Component,false,false,g1,2,,",
            "PP-MUN01,0.5,KG,0,,,ByProduct,,,,,,");

        result.Errors.Should().BeEmpty();
        result.CreatedId.Should().NotBeNull();
        result.Version.Should().Be(1);
        var bom = c.F.Boms.Single();
        bom.Status.Should().Be(VersionStatus.Draft);
        bom.Lines.Should().HaveCount(5);
        bom.Lines.First().IssueMethod.Should().Be(IssueMethod.Backflush);
        bom.Lines.Single(l => l.ComponentCode == "NVL-KEO02").AlternativeGroup.Should().Be("G1");
        bom.Lines.Single(l => l.ComponentCode == "NVL-SON01").Notes.Should().Be("Two coats");
        bom.Lines.Single(l => l.ComponentCode == "PP-MUN01").LineType.Should().Be(BomLineType.ByProduct);
    }

    [Fact]
    public async Task A_Dry_Run_Reports_Everything_And_Writes_Nothing()
    {
        var c = Seeded();

        var result = await ImportBom(c, true, BomHeader, "NVL-VIT01,16,PCS,0,10,,,,,,,,");

        result.DryRun.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.CreatedId.Should().BeNull();
        c.F.Boms.Should().BeEmpty();
    }

    [Fact]
    public async Task TC_BOM_053_One_Unknown_Item_On_Line_5_Of_The_File_Reports_That_Line_And_Writes_Nothing()
    {
        var c = Seeded();
        var lines = new List<string> { BomHeader };
        lines.AddRange(Enumerable.Range(0, 3).Select(i => $"NVL-VIT01,{i + 1},PCS,0,{10 * (i + 1)},,,,,,,,"));
        lines.Add("NO-SUCH-ITEM,1,PCS,0,40,,,,,,,,");              // file line 5 = the 4th data row
        lines.AddRange(Enumerable.Range(0, 6).Select(i => $"NVL-THUNG01,{i + 1},PCS,0,{100 + i},,,,,,,,"));

        var result = await ImportBom(c, false, lines.ToArray());

        result.RowCount.Should().Be(10);
        result.Errors.Should().ContainSingle().Which.Should().Match<ImportRowError>(e => e.Line == 5 && e.Key == "NO-SUCH-ITEM" && e.Message.Contains("NO-SUCH-ITEM"));
        result.CreatedId.Should().BeNull();
        c.F.Boms.Should().BeEmpty("one bad line means no document at all");
        c.F.Saves.Should().Be(0);
    }

    [Fact]
    public async Task Every_Problem_Of_The_File_Is_Reported_Together_With_Its_Line()
    {
        var c = Seeded();

        var result = await ImportBom(c, false, BomHeader,
            "NVL-VIT01,abc,PCS,0,10,,,,,,,,",           // 2: quantity not a number
            "NVL-SON01,0.5,XYZ,0,20,,,,,,,,",           // 3: unknown unit
            "NVL-SON01,0.5,PCS,0,20,,,,,,,,",           // 4: paint in pieces
            "NVL-VIT01,16,PCS,150,10,,,,,,,,",          // 5: scrap above 99.99
            "NVL-VIT01,16,PCS,0,10,Sometimes,,,,,,,",   // 6: unknown issue method
            ",1,PCS,0,10,,,,,,,,",                      // 7: no SKU
            "NVL-KEO01,0.1,KG,0,10,,,maybe,,,,,",       // 8: not a boolean
            "NVL-VIT01,16,PCS,0,10,,,,,,,,",            // 9: fine
            "NVL-VIT01,8,PCS,0,10,,,,,,,,");            // 10: same component twice at operation 10

        result.Errors.Select(e => e.Line).Should().Equal(2, 3, 4, 5, 6, 7, 8, 10);
        result.Errors.Single(e => e.Line == 4).Message.Should().Contain("NVL-SON01");
        result.Errors.Single(e => e.Line == 10).Message.Should().Contain("NVL-VIT01").And.Contain("10");
        c.F.Boms.Should().BeEmpty();
    }

    [Fact]
    public async Task A_SKU_Shared_By_Two_Products_Is_Refused_As_Ambiguous()
    {
        var c = Seeded();
        c.Item("NVL-VIT01", c.Piece); // a second product with the same SKU (SKU is not unique in ProductService, R149)

        var result = await ImportBom(c, false, BomHeader, "NVL-VIT01,16,PCS,0,10,,,,,,,,");

        result.Errors.Should().ContainSingle().Which.Message.Should().Contain("2 items");
        c.F.Boms.Should().BeEmpty();
    }

    [Fact]
    public async Task Headers_Match_Without_Case_Spaces_Or_Underscores_And_A_Semicolon_Works()
    {
        var c = Seeded();

        var result = await ImportBom(c, false, "Component SKU;QUANTITY;uom", "NVL-VIT01;16;PCS");

        result.Errors.Should().BeEmpty();
        c.F.Boms.Single().Lines.Should().ContainSingle();
    }

    [Fact]
    public async Task A_Comma_As_The_Decimal_Mark_Is_Accepted_In_A_Semicolon_File()
    {
        var c = Seeded();

        var result = await ImportBom(c, false, "componentSku;quantity;uom", "NVL-SON01;0,5;L");

        result.Errors.Should().BeEmpty();
        c.F.Boms.Single().Lines.Single().Quantity.Should().Be(0.5m);
    }

    [Fact]
    public async Task A_Missing_Required_Column_Is_A_400_Before_Any_Row_Is_Read()
    {
        var c = Seeded();

        var refusal = await FluentActions.Awaiting(() => ImportBom(c, false, "componentSku,quantity", "NVL-VIT01,16")).Should().ThrowAsync<BusinessRuleException>();

        refusal.Which.Message.Should().Contain("uom");
        c.F.Boms.Should().BeEmpty();
    }

    [Fact]
    public async Task A_File_With_Only_A_Header_Is_Refused()
    {
        var c = Seeded();

        await FluentActions.Awaiting(() => ImportBom(c, false, BomHeader)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task A_File_Over_The_Row_Limit_Is_Refused()
    {
        var c = Seeded();
        var lines = new List<string> { BomHeader };
        lines.AddRange(Enumerable.Range(1, 2001).Select(i => $"NVL-VIT01,1,PCS,0,{i},,,,,,,,"));

        await FluentActions.Awaiting(() => ImportBom(c, false, lines.ToArray())).Should().ThrowAsync<BusinessRuleException>();
        c.F.Boms.Should().BeEmpty();
    }

    [Fact]
    public async Task The_Second_Import_Is_The_Next_Version()
    {
        var c = Seeded();
        await ImportBom(c, false, BomHeader, "NVL-VIT01,16,PCS,0,10,,,,,,,,");

        var second = await ImportBom(c, false, BomHeader, "NVL-VIT01,18,PCS,0,10,,,,,,,,");

        second.Version.Should().Be(2);
        c.F.Boms.Select(b => b.Version).Should().Equal(1, 2);
    }

    [Fact]
    public async Task An_Item_That_Is_Bought_Cannot_Get_A_BOM_By_Import_Either()
    {
        var c = Seeded();

        var refusal = await FluentActions.Awaiting(() => c.BomImport.Handle(new ImportBomCommand(c.Plant.Id, c.Items["NVL-GO01"].Id, null, null, null, false,
            RoutingCtx.Csv(BomHeader, "NVL-VIT01,1,PCS,0,10,,,,,,,,")), default)).Should().ThrowAsync<BusinessRuleException>();

        refusal.Which.RuleName.Should().Be(MfgErrors.Bom001);
    }

    [Fact]
    public async Task The_Template_Itself_Imports_Cleanly()
    {
        var c = Seeded();
        var body = new MemoryStream(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(BomCsvTemplate.Csv)).ToArray());

        var result = await c.BomImport.Handle(new ImportBomCommand(c.Plant.Id, c.Items["TP-BAN01"].Id, null, null, null, false, body), default);

        result.Errors.Should().BeEmpty();
        c.F.Boms.Single().Lines.Should().HaveCount(2);
    }

    // ── Routing ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_Clean_Routing_File_Creates_A_Draft_Whose_Lead_Time_Is_The_DS01_One()
    {
        var c = Seeded();

        var result = await ImportRouting(c, false, RoutingHeader,
            "10,Assembly,WC-ASM,,15,10,PerUnit,,0,0,false,",
            "20,Painting,wc-pnt,,30,6,,,0,0,false,Two coats",
            "30,Packing,WC-PKG,,0,2,PerUnit,,0,0,false,");

        result.Errors.Should().BeEmpty();
        var routing = c.F.Routings.Single();
        routing.Status.Should().Be(VersionStatus.Draft);
        routing.Operations.Should().HaveCount(3);
        routing.Operations.Single(o => o.OperationSeq == 20).Instructions.Should().Be("Two coats");
        (await c.Time.LeadTimeAsync(routing, 100, new DateOnly(2026, 11, 13))).TotalMinutes.Should().Be(2003);
    }

    [Fact]
    public async Task A_Routing_File_With_Problems_Reports_Every_Line_And_Writes_Nothing()
    {
        var c = Seeded();
        c.Standard("PNT-STD", "WC-PNT", 30, 6);

        var result = await ImportRouting(c, false, RoutingHeader,
            "ten,Bad,WC-ASM,,0,1,,,,,,",             // 2: sequence not a number
            "20,Unknown center,WC-NOPE,,0,1,,,,,,",   // 3: unknown work center
            "30,Unknown std,WC-ASM,STD-NOPE,0,1,,,,,,", // 4: unknown standard operation
            "40,Batch,WC-ASM,,0,5,PerBatch,,,,,",     // 5: batch without a size
            "50,Zero run,WC-ASM,,0,0,,,,,,",          // 6: run time 0
            "60,Outside,WC-ASM,,0,1,,,,,true,",       // 7: subcontract on a normal center
            "70,Fine,WC-ASM,,0,1,,,,,,",              // 8: fine
            "70,Twice,WC-ASM,,0,1,,,,,,",             // 9: duplicate sequence
            "80,Std,WC-PNT,PNT-STD,,,,,,,,");         // 10: fine, values come from the standard operation

        result.Errors.Select(e => e.Line).Should().Equal(2, 3, 4, 5, 6, 7, 9);
        c.F.Routings.Should().BeEmpty();
    }

    [Fact]
    public async Task A_Standard_Operation_In_The_File_Fills_The_Missing_Times()
    {
        var c = Seeded();
        c.Standard("PNT-STD", "WC-PNT", 30, 6);

        var result = await ImportRouting(c, false, RoutingHeader, "10,,WC-PNT,PNT-STD,,,,,,,,");

        result.Errors.Should().BeEmpty();
        c.F.Routings.Single().Operations.Single().Should().Match<RoutingOperation>(o => o.SetupTimeMinutes == 30 && o.RunTimeMinutes == 6 && o.Name == "Standard PNT-STD");
    }

    [Fact]
    public async Task A_Routing_Dry_Run_Writes_Nothing_And_The_Next_Import_Is_Version_2()
    {
        var c = Seeded();

        (await ImportRouting(c, true, RoutingHeader, "10,A,WC-ASM,,0,1,,,,,,")).CreatedId.Should().BeNull();
        c.F.Routings.Should().BeEmpty();

        await ImportRouting(c, false, RoutingHeader, "10,A,WC-ASM,,0,1,,,,,,");
        (await ImportRouting(c, false, RoutingHeader, "10,A,WC-ASM,,0,2,,,,,,")).Version.Should().Be(2);
    }

    [Fact]
    public async Task The_Routing_Template_Imports_Cleanly()
    {
        var c = Seeded();
        var body = new MemoryStream(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(RoutingCsvTemplate.Csv)).ToArray());

        var result = await c.RoutingImport.Handle(new ImportRoutingCommand(c.Plant.Id, c.Items["TP-BAN01"].Id, null, false, body), default);

        result.Errors.Should().BeEmpty();
        c.F.Routings.Single().Operations.Should().HaveCount(2);
    }
}

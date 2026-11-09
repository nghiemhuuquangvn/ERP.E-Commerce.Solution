using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Infrastructure.Services;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.MFG.UnitTests.Orders;

/// <summary>BR-PO-01/03/04/05 on the DS-01 data (TC-PO-001..010).</summary>
public class CreateOrderTests
{
    private static async Task<OrderCtx> SeededAsync()
    {
        var ctx = new OrderCtx();
        await ctx.SeedAllAsync();
        return ctx;
    }

    private static decimal Required(OrderDto order, string code) => order.Materials.Single(m => m.ComponentCode == code).RequiredQty;

    [Fact]
    public async Task TC_PO_001_A_Standard_Order_Gets_Its_Number_The_Approved_Versions_And_The_Snapshot()
    {
        var ctx = await SeededAsync();

        var order = await ctx.CreateOrderAsync();

        order.OrderNo.Should().Be("MO-2611-00001");
        order.Status.Should().Be(OrderStatus.Draft);
        order.Priority.Should().Be(3);
        order.BomId.Should().Be(ctx.BomIds["TP-BAN01"]);
        order.RoutingId.Should().NotBeNull();
        order.Materials.Should().HaveCount(7);
        Required(order, "BTP-MAT01").Should().Be(100m);
        Required(order, "BTP-CHAN01").Should().Be(400m);
        Required(order, "NVL-VIT01").Should().Be(1600m);
        Required(order, "NVL-SON01").Should().Be(52.5m);
        Required(order, "NVL-KEO01").Should().Be(10m);
        Required(order, "NVL-THUNG01").Should().Be(100m);
        Required(order, "NVL-XOP01").Should().Be(200m);
        order.Materials.Select(m => m.ComponentCode).Should().NotContain(["NVL-KEO02", "KIT-PKG01"]);
        order.Materials.Single(m => m.ComponentCode == "NVL-VIT01").IssueMethod.Should().Be(IssueMethod.Backflush);
        order.Materials.Single(m => m.ComponentCode == "NVL-SON01").ScrapPct.Should().Be(5m);
        order.Materials.Should().OnlyContain(m => m.WarehouseId == ctx.Plant.RawMaterialWarehouseId);
        order.Operations.Select(o => o.OperationSeq).Should().Equal(10, 20, 30);
        order.Operations.Should().OnlyContain(o => o.Status == OperationStatus.Pending);
        order.Outputs.Should().BeEmpty();
        order.OutputWarehouseId.Should().Be(ctx.Plant.FinishedGoodsWarehouseId);
        order.Warnings.Should().BeEmpty();
        ctx.Repo.Orders.Should().ContainSingle();
    }

    [Fact]
    public async Task The_Materials_Follow_The_Operations_They_Belong_To()
    {
        var ctx = await SeededAsync();

        var order = await ctx.CreateOrderAsync();

        order.Materials.Single(m => m.ComponentCode == "NVL-SON01").OperationSeq.Should().Be(20);
        order.Materials.Single(m => m.ComponentCode == "NVL-THUNG01").OperationSeq.Should().Be(30);   // through the phantom kit
        order.Materials.Single(m => m.ComponentCode == "NVL-KEO01").OperationSeq.Should().Be(10);
    }

    [Fact]
    public async Task A_Material_Whose_Operation_Is_Not_In_The_Routing_Is_Attached_To_The_First_One_With_A_Warning()
    {
        var ctx = await SeededAsync();
        var routing = await ctx.NewRoutingAsync("BTP-MAT01");
        await ctx.AddOpAsync(routing.Id, 20, "Cut", "WC-CUT", 20, 3);   // the BOM line says operation 10
        await ctx.ApproveRoutingAsync(routing.Id);

        var order = await ctx.CreateOrderAsync("BTP-MAT01", 50, new DateOnly(2026, 11, 13));

        order.Materials.Single().OperationSeq.Should().Be(20);
        order.Warnings.Should().ContainSingle().Which.Should().Contain("NVL-GO01");
    }

    [Fact]
    public async Task By_Products_Of_The_Bom_Become_Outputs()
    {
        var ctx = await SeededAsync();

        var order = await ctx.CreateOrderAsync("BTP-MAT01", 50);

        order.Outputs.Should().ContainSingle().Which.Should().Match<OrderOutputDto>(o => o.ItemCode == "PP-MUN01" && o.PlannedQty == 25m && o.LineType == BomLineType.ByProduct);
        Required(order, "NVL-GO01").Should().Be(1.1m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task TC_PO_002_A_Quantity_Of_Zero_Or_Less_Is_Refused(int quantity)
    {
        var ctx = await SeededAsync();

        var act = () => ctx.CreateOrderAsync(quantity: quantity);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po001);
        ctx.Repo.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task TC_PO_003_A_Fraction_Of_A_Unit_That_Has_No_Decimals_Is_Refused()
    {
        var ctx = await SeededAsync();

        var act = () => ctx.CreateOrderAsync(quantity: 10.5m);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Message.Should().Contain("decimal");
    }

    [Fact]
    public async Task A_Unit_With_Decimals_Takes_A_Fraction()
    {
        var ctx = await SeededAsync();
        ctx.Item("PRD-KG", ctx.Kg);
        ctx.Profile("PRD-KG", MakeOrBuy.Make);
        var bom = await ctx.CreateAsync("PRD-KG");
        await ctx.AddAsync(bom.Id, "NVL-KEO01", 1, ctx.Kg);
        await ctx.ApproveAsync(bom.Id);

        var order = await ctx.CreateOrderAsync("PRD-KG", 10.25m);

        order.Quantity.Should().Be(10.25m);
        Required(order, "NVL-KEO01").Should().Be(10.25m);
    }

    [Fact]
    public async Task TC_PO_004_A_Due_Date_In_The_Past_Is_Refused()
    {
        var ctx = await SeededAsync();

        var act = () => ctx.CreateOrderAsync(due: new DateOnly(2026, 11, 3));

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po003);
    }

    [Fact]
    public async Task TC_PO_005_A_Due_Date_Of_Today_Is_Accepted_And_The_Order_Is_Late()
    {
        var ctx = await SeededAsync();

        var order = await ctx.CreateOrderAsync(due: new DateOnly(2026, 11, 4));

        order.IsLate.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public async Task TC_PO_006_An_Item_Without_An_Effective_Bom_Is_Refused_With_The_Date()
    {
        var ctx = await SeededAsync();
        ctx.Item("TP-NEW", ctx.Piece);
        ctx.Profile("TP-NEW", MakeOrBuy.Make);
        var bom = await ctx.CreateAsync("TP-NEW", from: new DateOnly(2027, 1, 1));
        await ctx.AddAsync(bom.Id, "NVL-VIT01", 4, ctx.Piece);
        await ctx.ApproveAsync(bom.Id);

        var act = () => ctx.CreateOrderAsync("TP-NEW");

        var error = (await act.Should().ThrowAsync<BusinessRuleException>()).Which;
        error.RuleName.Should().Be(MfgErrors.Po004);
        error.Message.Should().Contain("2026-11-04");
    }

    [Fact]
    public async Task TC_PO_007_A_Chosen_Bom_Version_That_Is_Not_Effective_Is_Refused_And_The_Default_Is_The_Effective_One()
    {
        var ctx = await SeededAsync();
        var v2 = await ctx.CopyAsync(ctx.BomIds["TP-BAN01"], new DateOnly(2026, 12, 1));
        await ctx.ApproveAsync(v2.Id);

        var chosen = () => ctx.CreateOrderAsync(bomId: v2.Id);
        (await chosen.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po005);

        var byDefault = await ctx.CreateOrderAsync();
        byDefault.BomId.Should().Be(ctx.BomIds["TP-BAN01"]);
    }

    [Fact]
    public async Task A_Draft_BOM_Cannot_Be_Chosen()
    {
        var ctx = await SeededAsync();
        var draft = await ctx.CopyAsync(ctx.BomIds["TP-BAN01"], new DateOnly(2026, 11, 1));

        var act = () => ctx.CreateOrderAsync(bomId: draft.Id);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po005);
    }

    [Fact]
    public async Task TC_PO_008_Changing_The_Quantity_Of_A_Draft_Regenerates_The_Snapshot()
    {
        var ctx = await SeededAsync();
        var order = await ctx.CreateOrderAsync();

        var changed = await ctx.UpdateOrderAsync(order.Id, 120, new DateOnly(2026, 11, 13));

        Required(changed, "NVL-SON01").Should().Be(63m);
        Required(changed, "BTP-CHAN01").Should().Be(480m);
        changed.Operations.Single(o => o.OperationSeq == 10).StandardMinutes.Should().Be(1215m);   // 15 + 120 x 10
        changed.Materials.Should().HaveCount(7);
        changed.Operations.Should().HaveCount(3);
        ctx.Stored(order.Id).AllMaterials.Count(m => !m.IsDeleted).Should().Be(7);
        ctx.Stored(order.Id).AllMaterials.Count(m => m.IsDeleted).Should().Be(7);   // the old lines are retired, not kept
    }

    [Fact]
    public async Task TC_PO_009_The_Snapshot_Does_Not_Follow_A_Later_Bom_Version()
    {
        var ctx = await SeededAsync();
        var order = await ctx.CreateOrderAsync();
        var v2 = await ctx.CopyAsync(ctx.BomIds["TP-BAN01"], new DateOnly(2026, 11, 5));
        await ctx.AddAsync(v2.Id, "NVL-GO01", 1, ctx.M3);
        await ctx.ApproveAsync(v2.Id);

        var reread = ctx.Stored(order.Id);

        reread.Materials.Should().HaveCount(7);
        reread.BomId.Should().Be(ctx.BomIds["TP-BAN01"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task TC_PO_010_A_Priority_Outside_One_To_Five_Is_Refused(int priority)
    {
        var ctx = await SeededAsync();

        var act = () => ctx.CreateOrderAsync(priority: priority);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Message.Should().Contain("priority");
    }

    [Fact]
    public async Task A_Buy_Item_Cannot_Be_Produced()
    {
        var ctx = await SeededAsync();

        var act = () => ctx.CreateOrderAsync("NVL-SON01");

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Itm001);
    }

    [Fact]
    public async Task An_Inactive_Item_Is_Refused()
    {
        var ctx = await SeededAsync();
        ctx.Replace("TP-BAN01", p => p with { IsActive = false });

        var act = () => ctx.CreateOrderAsync();

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po006);
    }

    [Fact]
    public async Task An_Inactive_Component_Is_Refused_And_Named()
    {
        var ctx = await SeededAsync();
        ctx.Replace("NVL-KEO01", p => p with { IsActive = false });

        var act = () => ctx.CreateOrderAsync();

        var error = (await act.Should().ThrowAsync<BusinessRuleException>()).Which;
        error.RuleName.Should().Be(MfgErrors.Po006);
        error.Message.Should().Contain("NVL-KEO01");
    }

    [Fact]
    public async Task A_Serial_Controlled_Item_Is_Refused()
    {
        var ctx = await SeededAsync();
        ctx.Replace("TP-BAN01", p => p with { IsSerialControlled = true });

        var act = () => ctx.CreateOrderAsync();

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat011);
    }

    [Fact]
    public async Task A_Serial_Controlled_Component_Is_Refused()
    {
        var ctx = await SeededAsync();
        ctx.Replace("NVL-VIT01", p => p with { IsSerialControlled = true });

        var act = () => ctx.CreateOrderAsync();

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Mat011);
    }

    [Fact]
    public async Task A_Plant_The_Caller_May_Not_See_Does_Not_Exist_For_Them()
    {
        var ctx = await SeededAsync();
        var stranger = new PlantAccessService(ctx.F.PlantRepo, ctx.F.AccessRepo, new ERP.MFG.UnitTests.Common.FakeUser(Guid.NewGuid()));

        var act = () => ctx.CreateOrderAsync(access: stranger);

        await act.Should().ThrowAsync<EntityNotFoundException>();
        ctx.Repo.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task Order_Numbers_Count_Per_Month()
    {
        var ctx = await SeededAsync();

        var first = await ctx.CreateOrderAsync();
        var second = await ctx.CreateOrderAsync();
        ctx.Clock.Now = new DateTimeOffset(2026, 12, 1, 1, 0, 0, TimeSpan.Zero);
        var third = await ctx.CreateOrderAsync(due: new DateOnly(2026, 12, 15));

        new[] { first.OrderNo, second.OrderNo, third.OrderNo }.Should().Equal("MO-2611-00001", "MO-2611-00002", "MO-2612-00001");
    }

    [Fact]
    public async Task The_Order_Number_Counts_Per_Tenant_Not_Per_Plant_Because_The_Warehouse_Deduplicates_On_It()
    {
        var ctx = await SeededAsync();
        var recorder = new RecordingNumbers();
        var handler = new CreateOrderHandler(ctx.Access, ctx.Builder(), ctx.Repo, recorder, ctx.PlantTime, ctx.User, ctx.F.Uow);

        await handler.Handle(new CreateOrderCommand(ctx.Plant.Id, ctx.Items["TP-BAN01"].Id, 100, new DateOnly(2026, 11, 13), null, null, null, null, null), default);

        recorder.PlantIds.Should().Equal(Guid.Empty);
    }

    private sealed class RecordingNumbers : ERP.MFG.Application.Abstractions.IDocumentNumberService
    {
        public readonly List<Guid> PlantIds = [];
        public Task<string> NextAsync(string docType, Guid plantId, DateTime businessDate, CancellationToken ct = default)
        {
            PlantIds.Add(plantId);
            return Task.FromResult(DocumentTypes.Format(docType, businessDate, 1));
        }
    }

    [Fact]
    public async Task Only_A_Draft_Can_Be_Changed()
    {
        var ctx = await SeededAsync();
        var order = await ctx.CreateOrderAsync();
        ctx.Stored(order.Id).TransitionTo(OrderStatus.Released, "u");

        var act = () => ctx.UpdateOrderAsync(order.Id, 120, new DateOnly(2026, 11, 13));

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(MfgErrors.Po002);
    }

    [Fact]
    public async Task Changing_Only_The_Priority_Keeps_The_Same_Lines()
    {
        var ctx = await SeededAsync();
        var order = await ctx.CreateOrderAsync();
        var lineIds = ctx.Stored(order.Id).AllMaterials.Select(m => m.Id).ToList();

        var changed = await ctx.UpdateOrderAsync(order.Id, 100, new DateOnly(2026, 11, 13), priority: 1);

        changed.Priority.Should().Be(1);
        ctx.Stored(order.Id).AllMaterials.Select(m => m.Id).Should().Equal(lineIds);
    }
}

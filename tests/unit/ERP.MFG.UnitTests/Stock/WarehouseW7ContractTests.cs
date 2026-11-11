using System.Net;
using System.Text;
using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Seeding;
using ERP.MFG.Infrastructure.Clients;
using ERP.MFG.UnitTests.Orders;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.MFG.UnitTests.Stock;

/// <summary>
/// Warehouse W7 as Manufacturing sees it (P4-1): the movement answer carries the lots really used, a lot row knows its parent, and a rework
/// issue may name a lot on quality hold. Nothing in Manufacturing uses these yet (P4-2 .. P4-5); this fixes the contract and the fake.
/// </summary>
public class WarehouseW7ContractTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public readonly List<string> Bodies = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            return respond(request);
        }
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static (WarehouseStockClient Client, StubHandler Handler) Make(string answer)
    {
        var handler = new StubHandler(_ => Json(answer));
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(t => t.CurrentTenantId).Returns(Guid.NewGuid());
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://warehouse/") };
        return (new WarehouseStockClient(http, new MemoryCache(new MemoryCacheOptions()), tenant.Object, NullLogger<WarehouseStockClient>.Instance), handler);
    }

    private static readonly Guid LotA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid LotB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Parent = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task A_multi_lot_answer_lists_each_lot_with_its_cost_and_a_split_answer_names_the_parent()
    {
        var (client, _) = Make("""
            {"movementId":"11111111-1111-1111-1111-111111111111","movementType":"ProductionIssue","quantity":52.5,"recordedAt":"2026-11-11T01:00:00Z","unitCost":10.857143,"valueDelta":-570,
             "warehouseId":null,"lotId":null,"replayed":false,
             "lots":[{"lotId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","lotNumber":"L-A","quantity":30,"unitCost":10,"warehouseId":null,"parentLotId":null},
                     {"lotId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","lotNumber":"L-B","quantity":22.5,"unitCost":12,"warehouseId":null,"parentLotId":"cccccccc-cccc-cccc-cccc-cccccccccccc"}]}
            """);

        var result = await client.RecordMovementAsync(new StockMovementRequest("ProductionIssue", Guid.NewGuid(), 52.5m, "MO-1#1"));

        result.LotId.Should().BeNull("an issue over several lots names none");
        result.Lots.Should().HaveCount(2);
        result.Lots![0].Should().Be(new StockMovementLotInfo(LotA, "L-A", 30m, 10m, null, null));
        result.Lots[1].Should().Be(new StockMovementLotInfo(LotB, "L-B", 22.5m, 12m, null, Parent));
    }

    [Fact]
    public async Task An_answer_from_before_W7_has_no_lots_and_still_reads()
    {
        var (client, _) = Make("""{"movementId":"11111111-1111-1111-1111-111111111111","movementType":"ProductionIssue","quantity":3,"recordedAt":"2026-11-10T01:00:00Z","unitCost":10,"valueDelta":-30,"warehouseId":null,"lotId":null,"replayed":false}""");

        var result = await client.RecordMovementAsync(new StockMovementRequest("ProductionIssue", Guid.NewGuid(), 3m, "MO-1#1"));

        result.Lots.Should().BeNull();
        result.UnitCost.Should().Be(10m);
    }

    [Fact]
    public async Task The_rework_flag_travels_in_the_request_and_a_lot_row_reads_its_parent()
    {
        var (client, handler) = Make("""{"movementId":"11111111-1111-1111-1111-111111111111","movementType":"ProductionIssue","quantity":2,"recordedAt":"2026-11-11T01:00:00Z","replayed":false}""");
        await client.RecordMovementAsync(new StockMovementRequest("ProductionIssue", Guid.NewGuid(), 2m, "RW-1#1", LotId: LotA, Reason: "Rework RW-1", AllowHeldLot: true));
        handler.Bodies.Single().Should().Contain("\"allowHeldLot\":true").And.Contain("\"reason\":\"Rework RW-1\"");

        var (lots, _) = Make("""[{"lotId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","lotNumber":"L-A-S1","warehouseId":null,"status":"QcHold","remainingQuantity":4,"expiryDate":null,"manufactureDate":null,"parentLotId":"cccccccc-cccc-cccc-cccc-cccccccccccc"}]""");
        (await lots.ListLotsAsync(Guid.NewGuid())).Single().ParentLotId.Should().Be(Parent);
    }

    // ── the fake Warehouse used by the order tests follows the same rules ───────────────────────────

    private static readonly Guid Product = Guid.NewGuid();
    private static readonly Guid Quarantine = Guid.NewGuid();
    private static readonly Guid Finished = Guid.NewGuid();

    [Fact]
    public async Task The_fake_warehouse_splits_a_lot_on_a_partial_transfer_and_keeps_the_hold()
    {
        var stock = new FakeStock();
        var lot = stock.Lot(Product, Quarantine, "FG-1", 10m, status: "QcHold");

        var moved = await stock.RecordMovementAsync(new StockMovementRequest("SubcontractTransfer", Product, 4m, "SC-1#1", WarehouseId: Quarantine, ToWarehouseId: Finished, LotId: lot));

        moved.LotId.Should().Be(lot, "the movement keeps the requested lot");
        var sub = moved.Lots!.Single();
        sub.ParentLotId.Should().Be(lot);
        sub.LotNumber.Should().Be("FG-1-S1");
        sub.WarehouseId.Should().Be(Finished);
        var rows = stock.Balances[Product];
        rows.Single(b => b.LotId == lot).Quantity.Should().Be(6m);
        rows.Single(b => b.LotId == sub.LotId).Should().Match<StockBalanceInfo>(b => b.Quantity == 4m && b.LotStatus == "QcHold");
        stock.Total(Product).Should().Be(10m);

        var replay = await stock.RecordMovementAsync(new StockMovementRequest("SubcontractTransfer", Product, 4m, "SC-1#1", WarehouseId: Quarantine, ToWarehouseId: Finished, LotId: lot));
        replay.Replayed.Should().BeTrue();
        replay.Lots!.Single().LotId.Should().Be(sub.LotId);
    }

    [Fact]
    public async Task The_lot_cycle_plays_split_release_and_rework_in_order_and_every_step_is_ok()
    {
        var stock = new FakeStock();
        var products = new Mock<IProductLookupClient>();
        products.Setup(p => p.LookupAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductInfo> { new(Product, "FG", "Chair", true, null, true) });

        var result = await new RunLotCycleHandler(stock, products.Object).Handle(new RunLotCycleCommand(new LotCycleRequest("MO-2611-00077", Product, Quarantine, Finished)), default);

        result.Steps.Where(s => !s.Ok).Should().BeEmpty("every step of the cycle passes: " + string.Join(" | ", result.Steps.Select(s => s.Name + ": " + s.Detail)));
        result.Steps.Select(s => s.Name).Should().ContainInOrder("receipt-on-hold", "split-transfer", "split-replay", "release-sub-lot", "hold-and-release-states", "issue-held-without-flag", "issue-held-for-rework");
        stock.Total(Product).Should().Be(8m, "10 received, 2 issued for rework; the split moved 4 without changing the total");
    }

    [Fact]
    public async Task The_fake_warehouse_issues_a_held_lot_only_on_request()
    {
        var stock = new FakeStock();
        var lot = stock.Lot(Product, Quarantine, "FG-2", 10m, status: "QcHold");

        var plain = () => stock.RecordMovementAsync(new StockMovementRequest("ProductionIssue", Product, 2m, "RW-1#1", LotId: lot));
        (await plain.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("WH_LOT_NOT_USABLE");

        var rework = await stock.RecordMovementAsync(new StockMovementRequest("ProductionIssue", Product, 2m, "RW-1#1", LotId: lot, Reason: "Rework", AllowHeldLot: true));
        rework.Lots!.Single().LotId.Should().Be(lot);
        stock.Total(Product).Should().Be(8m);
    }
}

using System.Text.Json;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Commands.Locations;
using ERP.WH.Application.Commands.Stocktakes;
using ERP.WH.Application.Commands.Transfers;
using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Queries.Inventory;
using ERP.WH.Application.Services;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Aggregates.Location;
using ERP.WH.Infrastructure.Persistence;
using ERP.WH.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using InventoryEntity = ERP.WH.Domain.Aggregates.Inventory.Inventory;

namespace ERP.WH.UnitTests.Ledger;

/// <summary>
/// W7 (Manufacturing Phase 4, P4-1): a partial move of a lot becomes a sub-lot, a production movement keeps the lots it really used, and a lot on
/// quality hold can be issued on request (rework). Real <see cref="WarehouseDbContext"/> on EF InMemory; parity (item total = balances = lots) after each scenario.
/// docs/business/Warehouse/ProductionIntegration.md.
/// </summary>
public class LotSplitAndBreakdownTests
{
    private sealed class FixedTenant(Guid id) : ITenantProvider
    {
        public Guid? CurrentTenantId => id;
    }

    private readonly ServiceProvider _sp;
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _main;
    private readonly Guid _north;

    public LotSplitAndBreakdownTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantProvider>(new FixedTenant(_tenant));
        var database = Guid.NewGuid().ToString();
        services.AddDbContext<WarehouseDbContext>(o => o.UseInMemoryDatabase(database));
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<IInventoryLotRepository, InventoryLotRepository>();
        services.AddScoped<IInventorySerialRepository, InventorySerialRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<ITransferOrderRepository, TransferOrderRepository>();
        services.AddScoped<IStocktakeRepository, StocktakeRepository>();
        services.AddScoped<IInventoryLocationStockRepository, InventoryLocationStockRepository>();
        services.AddScoped<IStockAdjustmentRepository, StockAdjustmentRepository>();
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<IBarcodeMappingRepository, BarcodeMappingRepository>();
        services.AddScoped<ICostConsumptionLogRepository, CostConsumptionLogRepository>();
        services.AddScoped<IStockBalanceRepository, StockBalanceRepository>();
        services.AddScoped<IWarehouseResolver, WarehouseResolver>();
        services.AddScoped<IWarehouseUnitOfWork, WarehouseUnitOfWork>();
        services.AddScoped<IStockLedger, StockLedger>();
        services.AddScoped<IProductionMovementService, ProductionMovementService>();
        services.AddSingleton(new Mock<ICacheService>().Object);
        _sp = services.BuildServiceProvider();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
        var main = new Warehouse("MAIN", "Main");
        var north = new Warehouse("NORTH", "North");
        db.Warehouses.AddRange(main, north);
        db.SaveChanges();
        _main = main.Id;
        _north = north.Id;
    }

    private sealed record Ctx(WarehouseDbContext Db, IWarehouseUnitOfWork Uow, IStockLedger Ledger, IProductionMovementService Production, ICacheService Cache);

    private Ctx Open()
    {
        var p = _sp.CreateScope().ServiceProvider;
        return new Ctx(p.GetRequiredService<WarehouseDbContext>(), p.GetRequiredService<IWarehouseUnitOfWork>(), p.GetRequiredService<IStockLedger>(),
            p.GetRequiredService<IProductionMovementService>(), p.GetRequiredService<ICacheService>());
    }

    /// <summary>An item holding the given lots at MAIN: (number, quantity, cost, days to expiry), valued as asked.</summary>
    private async Task<(Guid Product, Dictionary<string, Guid> Lots)> SeedAsync(ValuationMethod method, params (string Number, decimal Qty, decimal Cost, int Days)[] lots)
    {
        var c = Open();
        var item = new InventoryEntity(Guid.NewGuid(), "Paint", 0, 0);
        item.SetValuationMethod(method);
        await c.Uow.Inventories.AddAsync(item, default);
        var ids = new Dictionary<string, Guid>();
        foreach (var l in lots)
        {
            var created = await c.Ledger.ReceiveAsync(item, l.Qty, l.Cost, new StockTarget(_main, null), new NewLot(l.Number, l.Cost, DateTime.UtcNow.AddDays(l.Days), null), default);
            ids[l.Number] = created!.LotId;
        }
        await c.Uow.SaveChangesAsync(default);
        return (item.ProductId, ids);
    }

    private async Task<(Guid Product, Guid Lot)> SeedOneLotAsync(string number = "L-1")
    {
        var (product, lots) = await SeedAsync(ValuationMethod.Average, (number, 10m, 10m, 60));
        return (product, lots[number]);
    }

    private async Task AssertParityAsync(Guid productId)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
        var inventory = await db.Inventories.SingleAsync(i => i.ProductId == productId);
        var balances = await db.StockBalances.Where(b => b.ProductId == productId).ToListAsync();
        balances.Sum(b => b.Quantity).Should().Be(inventory.TotalQuantity, "the balances add up to the item total");
        foreach (var lot in await db.InventoryLots.Where(l => l.ProductId == productId).ToListAsync())
        {
            var rows = balances.Where(b => b.LotId == lot.LotId).ToList();
            rows.Sum(b => b.Quantity).Should().Be(lot.RemainingQuantity, $"lot {lot.LotNumber} equals its balance");
            rows.Where(b => b.Quantity != 0).All(b => b.WarehouseId == lot.WarehouseId).Should().BeTrue($"lot {lot.LotNumber} is balanced in the warehouse it sits in");
        }
    }

    private static async Task<ProductionMovementResult> RunAsync(Ctx c, ProductionMovementRequest request)
    {
        var result = await c.Production.RecordAsync(request, default);
        await c.Uow.SaveChangesAsync(default);
        return result;
    }

    private async Task<InventoryLot> LotAsync(Guid id) => await Open().Db.InventoryLots.SingleAsync(l => l.LotId == id);

    private async Task<Guid> ReserveAsync(Guid product, decimal quantity)
    {
        var c = Open();
        var reservation = await new ReserveForSourceCommandHandler(c.Uow, c.Ledger, c.Cache).Handle(new ReserveForSourceCommand
        {
            SourceId = Guid.NewGuid(), WarehouseId = _main, Lines = [new SourceReservationLineDto { ProductId = product, Quantity = quantity }]
        }, default);
        return reservation.ReservationId;
    }

    private static async Task ExpectAsync(Func<Task> act, string code)
        => (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be(code);

    // ── lot split ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_part_of_a_lot_leaves_as_a_sub_lot_in_the_target_warehouse_and_the_total_does_not_change()
    {
        var (product, lotId) = await SeedOneLotAsync();

        var r = await RunAsync(Open(), new(MovementType.SubcontractTransfer, product, 4m, "SC-1#1", WarehouseId: _main, ToWarehouseId: _north, LotId: lotId));

        var parent = await LotAsync(lotId);
        parent.RemainingQuantity.Should().Be(6m);
        parent.ReceivedQuantity.Should().Be(6m, "the tree still adds up to the original receipt");
        parent.WarehouseId.Should().Be(_main);

        var sub = await Open().Db.InventoryLots.SingleAsync(l => l.ParentLotId == lotId);
        sub.LotNumber.Should().Be("L-1-S1");
        sub.RemainingQuantity.Should().Be(4m);
        sub.ReceivedQuantity.Should().Be(4m);
        sub.WarehouseId.Should().Be(_north);
        sub.Should().Match<InventoryLot>(s => s.UnitCost == parent.UnitCost && s.ExpiryDate == parent.ExpiryDate && s.ManufactureDate == parent.ManufactureDate
                                              && s.ReceivedDate == parent.ReceivedDate && s.Status == parent.Status && s.Source == parent.Source);

        r.StockDelta.Should().Be(0m);
        r.ValueDelta.Should().Be(0m);
        r.LotId.Should().Be(lotId, "the movement keeps the lot named in the request");
        r.Lots.Should().ContainSingle().Which.Should().BeEquivalentTo(new MovementLot(sub.LotId, "L-1-S1", 4m, 10m, _north, lotId));
        (await Open().Db.Inventories.SingleAsync(i => i.ProductId == product)).TotalQuantity.Should().Be(10m);
        var balances = await Open().Db.StockBalances.Where(b => b.ProductId == product).ToListAsync();
        balances.Single(b => b.LotId == lotId).Quantity.Should().Be(6m);
        balances.Single(b => b.LotId == sub.LotId).Should().Match<StockBalance>(b => b.Quantity == 4m && b.WarehouseId == _north);
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task The_next_split_gets_the_next_number_and_a_sub_lot_is_named_from_the_root()
    {
        var (product, lotId) = await SeedOneLotAsync();
        await RunAsync(Open(), new(MovementType.SubcontractTransfer, product, 3m, "SC-2#1", WarehouseId: _main, ToWarehouseId: _north, LotId: lotId));
        await RunAsync(Open(), new(MovementType.SubcontractTransfer, product, 2m, "SC-2#2", WarehouseId: _main, ToWarehouseId: _north, LotId: lotId));
        var first = await Open().Db.InventoryLots.SingleAsync(l => l.LotNumber == "L-1-S1");

        // a sub-lot is split again (back to MAIN): the number is built from the root, not from the sub-lot
        var again = await RunAsync(Open(), new(MovementType.SubcontractTransfer, product, 1m, "SC-2#3", WarehouseId: _north, ToWarehouseId: _main, LotId: first.LotId));

        again.Lots.Single().LotNumber.Should().Be("L-1-S3");
        (await Open().Db.InventoryLots.Where(l => l.ProductId == product).Select(l => l.LotNumber).ToListAsync())
            .Should().BeEquivalentTo("L-1", "L-1-S1", "L-1-S2", "L-1-S3");
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task The_number_of_a_used_up_sub_lot_is_never_reused()
    {
        var (product, lotId) = await SeedOneLotAsync();
        await RunAsync(Open(), new(MovementType.SubcontractTransfer, product, 4m, "SC-3#1", WarehouseId: _main, ToWarehouseId: _north, LotId: lotId));
        var sub = await Open().Db.InventoryLots.SingleAsync(l => l.LotNumber == "L-1-S1");
        await RunAsync(Open(), new(MovementType.ProductionScrap, product, 4m, "SC-3#2", LotId: sub.LotId));
        (await LotAsync(sub.LotId)).RemainingQuantity.Should().Be(0m);

        var next = await RunAsync(Open(), new(MovementType.SubcontractTransfer, product, 1m, "SC-3#3", WarehouseId: _main, ToWarehouseId: _north, LotId: lotId));

        next.Lots.Single().LotNumber.Should().Be("L-1-S2");
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task A_lot_on_hold_splits_into_a_sub_lot_on_hold_and_releasing_it_leaves_the_parent_held()
    {
        var (product, _) = await SeedOneLotAsync();
        var received = await RunAsync(Open(), new(MovementType.ProductionReceipt, product, 10m, "FR-1#1", WarehouseId: _main, LotNumber: "FG-1", UnitCost: 12m, InitialLotStatus: LotStatus.QcHold));
        var held = received.LotId!.Value;

        var split = await RunAsync(Open(), new(MovementType.SubcontractTransfer, product, 4m, "FR-1#2", WarehouseId: _main, ToWarehouseId: _north, LotId: held));
        var subId = split.Lots.Single().LotId;
        (await LotAsync(subId)).Status.Should().Be(LotStatus.QcHold, "a held lot's piece stays held until it is released");

        var release = await RunAsync(Open(), new(MovementType.QcRelease, product, 0m, "FR-1#3", LotId: subId));
        release.Movement.Quantity.Should().Be(4m);
        release.WarehouseId.Should().Be(_north);
        (await LotAsync(subId)).Status.Should().Be(LotStatus.Available);
        (await LotAsync(held)).Status.Should().Be(LotStatus.QcHold, "only the released piece changes");
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task Only_the_part_that_is_not_reserved_can_leave()
    {
        var (product, lotId) = await SeedOneLotAsync();
        await ReserveAsync(product, 7m);   // 7 of the lot's 10 are held by a production order

        var tooMuch = () => RunAsync(Open(), new(MovementType.SubcontractTransfer, product, 4m, "SC-4#1", WarehouseId: _main, ToWarehouseId: _north, LotId: lotId));
        await ExpectAsync(tooMuch, "WH_LOT_RESERVED");
        (await LotAsync(lotId)).RemainingQuantity.Should().Be(10m, "a refusal changes nothing");

        var free = await RunAsync(Open(), new(MovementType.SubcontractTransfer, product, 3m, "SC-4#2", WarehouseId: _main, ToWarehouseId: _north, LotId: lotId));
        free.Lots.Single().Quantity.Should().Be(3m);
        var parent = await LotAsync(lotId);
        parent.Should().Match<InventoryLot>(l => l.RemainingQuantity == 7m && l.ReservedQuantity == 7m, "the reservation stays on the lot it was made on");
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task More_than_the_lot_holds_is_refused()
    {
        var (product, lotId) = await SeedOneLotAsync();

        var act = () => RunAsync(Open(), new(MovementType.SubcontractTransfer, product, 11m, "SC-5#1", WarehouseId: _main, ToWarehouseId: _north, LotId: lotId));

        await ExpectAsync(act, "WH_LOT_QUANTITY");
        (await Open().Db.InventoryLots.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Moving_the_whole_lot_keeps_the_lot_and_carries_its_reservation()
    {
        var (product, lotId) = await SeedOneLotAsync();
        await ReserveAsync(product, 6m);

        var r = await RunAsync(Open(), new(MovementType.SubcontractTransfer, product, 10m, "SC-6#1", WarehouseId: _main, ToWarehouseId: _north, LotId: lotId));

        r.Lots.Single().Should().BeEquivalentTo(new MovementLot(lotId, "L-1", 10m, 10m, _north, null), "a whole move is the same lot, no sub-lot");
        var lot = await LotAsync(lotId);
        lot.Should().Match<InventoryLot>(l => l.WarehouseId == _north && l.ReservedQuantity == 6m && l.ParentLotId == null);
        (await Open().Db.StockBalances.SingleAsync(b => b.LotId == lotId && b.WarehouseId == _north)).ReservedQuantity.Should().Be(6m);
        (await Open().Db.StockBalances.SingleAsync(b => b.LotId == lotId && b.WarehouseId == _main)).Should().Match<StockBalance>(b => b.Quantity == 0m && b.ReservedQuantity == 0m, "the old warehouse keeps an empty row");
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task A_split_replays_to_the_same_sub_lot_and_another_quantity_under_the_key_is_a_conflict()
    {
        var (product, lotId) = await SeedOneLotAsync();
        var request = new ProductionMovementRequest(MovementType.SubcontractTransfer, product, 4m, "SC-7#1", WarehouseId: _main, ToWarehouseId: _north, LotId: lotId);
        var first = await RunAsync(Open(), request);

        var second = await RunAsync(Open(), request);

        second.Replayed.Should().BeTrue();
        second.Movement.MovementId.Should().Be(first.Movement.MovementId);
        second.Lots.Should().BeEquivalentTo(first.Lots, "the stored breakdown comes back");
        (await Open().Db.InventoryLots.CountAsync()).Should().Be(2, "the replay created no third lot");
        (await LotAsync(lotId)).RemainingQuantity.Should().Be(6m, "and moved nothing again");

        var other = () => RunAsync(Open(), request with { Quantity = 5m });
        (await other.Should().ThrowAsync<ConcurrencyException>()).Which.ErrorCode.Should().Be("WH_IDEMPOTENCY_CONFLICT");
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task A_sub_lot_number_longer_than_100_characters_is_refused_and_nothing_changes()
    {
        var (product, lotId) = await SeedOneLotAsync(new string('N', 98));

        var act = () => RunAsync(Open(), new(MovementType.SubcontractTransfer, product, 4m, "SC-8#1", WarehouseId: _main, ToWarehouseId: _north, LotId: lotId));

        await ExpectAsync(act, "WH_LOT_NUMBER_LENGTH");
        (await LotAsync(lotId)).RemainingQuantity.Should().Be(10m);
        (await Open().Db.InventoryLots.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_split_to_the_same_warehouse_and_lot_less_partial_moves_behave_as_before()
    {
        var (product, lotId) = await SeedOneLotAsync();
        var same = () => RunAsync(Open(), new(MovementType.SubcontractTransfer, product, 4m, "SC-9#1", WarehouseId: _main, ToWarehouseId: _main, LotId: lotId));
        await ExpectAsync(same, "WH_WAREHOUSE_SAME");

        var c = Open();
        var item = new InventoryEntity(Guid.NewGuid(), "Cut part", 0, 0);
        await c.Uow.Inventories.AddAsync(item, default);
        await c.Ledger.ReceiveAsync(item, 8m, 5m, new StockTarget(_main, null), null, default);
        await c.Uow.SaveChangesAsync(default);
        var r = await RunAsync(Open(), new(MovementType.SubcontractTransfer, item.ProductId, 3m, "SC-9#2", WarehouseId: _main, ToWarehouseId: _north));
        r.Lots.Should().BeEmpty("lot-less stock has no lot");
        await AssertParityAsync(item.ProductId);
    }

    [Fact]
    public void The_next_sub_lot_number_is_built_from_the_root_and_skips_every_number_in_use_whatever_its_case()
    {
        var product = Guid.NewGuid();
        var root = new InventoryLot(product, "AB-1", 10m, 1m);
        var s1 = root.SplitOff(2m, null, "AB-1-S1");
        var s2 = root.SplitOff(2m, null, "ab-1-s3");   // a number in another case still counts as used
        var grandchild = s1.SplitOff(1m, null, "AB-1-S2");

        StockLedger.NextSubLotNumber([root, s1, s2, grandchild], s1).Should().Be("AB-1-S4", "root AB-1; S1, S2 and S3 (any case) are taken");
        StockLedger.NextSubLotNumber([root], root).Should().Be("AB-1-S1");
        grandchild.ParentLotId.Should().Be(s1.LotId);
        s1.ParentLotId.Should().Be(root.LotId);
    }

    // ── the lots a movement used ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_reservation_issue_over_two_lots_keeps_each_lot_with_its_own_cost_and_the_value_adds_up()
    {
        var (product, lots) = await SeedAsync(ValuationMethod.FIFO, ("L-A", 30m, 10m, 10), ("L-B", 50m, 12m, 20));
        var reservation = await ReserveAsync(product, 52.5m);   // first expiry first: A 30 + B 22.5

        var r = await RunAsync(Open(), new(MovementType.ProductionIssue, product, 52.5m, "MO-1#1", ReservationId: reservation));

        r.LotId.Should().BeNull("an issue over several lots names none");
        r.Lots.Should().BeEquivalentTo(new[]
        {
            new MovementLot(lots["L-A"], "L-A", 30m, 10m, _main),
            new MovementLot(lots["L-B"], "L-B", 22.5m, 12m, _main)
        });
        r.Lots.Sum(l => l.Quantity).Should().Be(52.5m);
        r.ValueDelta.Should().Be(-570m, "30 x 10 + 22.5 x 12, the same figures as the rows");
        r.UnitCost.Should().Be(Math.Round(570m / 52.5m, 6));

        var replay = await RunAsync(Open(), new(MovementType.ProductionIssue, product, 52.5m, "MO-1#1", ReservationId: reservation));
        replay.Replayed.Should().BeTrue();
        replay.Lots.Should().BeEquivalentTo(r.Lots, o => o.WithStrictOrdering(), "the stored rows come back, in the same shape and the same order");
        replay.LotId.Should().BeNull();
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task Under_average_valuation_every_row_carries_the_item_cost()
    {
        var (product, lots) = await SeedAsync(ValuationMethod.Average, ("L-A", 30m, 10m, 10), ("L-B", 50m, 12m, 20));
        var cost = (await Open().Db.Inventories.SingleAsync(i => i.ProductId == product)).Cost;
        var reservation = await ReserveAsync(product, 40m);

        var r = await RunAsync(Open(), new(MovementType.ProductionIssue, product, 40m, "MO-2#1", ReservationId: reservation));

        r.Lots.Should().HaveCount(2);
        r.Lots.Should().OnlyContain(l => l.UnitCost == cost);
        r.ValueDelta.Should().Be(-Math.Round(40m * cost, 2));
    }

    [Fact]
    public async Task What_no_lot_covers_is_not_a_row()
    {
        var c = Open();
        var item = new InventoryEntity(Guid.NewGuid(), "Paint", 0, 0);
        item.SetValuationMethod(ValuationMethod.Average);
        await c.Uow.Inventories.AddAsync(item, default);
        var lot = await c.Ledger.ReceiveAsync(item, 10m, 10m, new StockTarget(_main, null), new NewLot("L-1", 10m, DateTime.UtcNow.AddDays(30), null), default);
        await c.Ledger.ReceiveAsync(item, 4m, 10m, new StockTarget(_main, null), null, default);   // 4 lot-less
        await c.Uow.SaveChangesAsync(default);
        var reservation = await ReserveAsync(item.ProductId, 12m);   // 10 from the lot, 2 lot-less

        var r = await RunAsync(Open(), new(MovementType.ProductionIssue, item.ProductId, 12m, "MO-3#1", ReservationId: reservation));

        r.Lots.Should().ContainSingle().Which.Quantity.Should().Be(10m);
        r.Lots.Single().LotId.Should().Be(lot!.LotId);
        r.Movement.Quantity.Should().Be(12m, "the movement is for 12; 2 of them came from the lot-less stock");
        await AssertParityAsync(item.ProductId);
    }

    [Fact]
    public async Task A_single_lot_movement_names_its_lot_in_both_places_and_lot_less_stock_has_no_rows()
    {
        var (product, lotId) = await SeedOneLotAsync();

        var issue = await RunAsync(Open(), new(MovementType.ProductionIssue, product, 2m, "MO-4#1", LotId: lotId));
        issue.LotId.Should().Be(lotId);
        issue.Lots.Should().ContainSingle().Which.Should().BeEquivalentTo(new MovementLot(lotId, "L-1", 2m, 10m, _main));

        var back = await RunAsync(Open(), new(MovementType.ProductionReturn, product, 1m, "MO-4#2", LotId: lotId, UnitCost: 10m));
        back.Lots.Should().ContainSingle().Which.LotId.Should().Be(lotId);

        var scrap = await RunAsync(Open(), new(MovementType.ProductionScrap, product, 1m, "MO-4#3", LotId: lotId));
        scrap.Lots.Should().ContainSingle().Which.Quantity.Should().Be(1m);

        var made = await RunAsync(Open(), new(MovementType.ProductionReceipt, product, 5m, "MO-4#4", WarehouseId: _main, LotNumber: "FG-1", UnitCost: 11m, InitialLotStatus: LotStatus.QcHold));
        made.Lots.Should().ContainSingle().Which.Should().BeEquivalentTo(new MovementLot(made.LotId!.Value, "FG-1", 5m, 11m, _main));

        var release = await RunAsync(Open(), new(MovementType.QcRelease, product, 0m, "MO-4#5", LotId: made.LotId));
        release.Lots.Should().ContainSingle().Which.Quantity.Should().Be(5m, "the release is for what is left in the lot");

        var noLot = await RunAsync(Open(), new(MovementType.ProductionReceipt, product, 3m, "MO-4#6", WarehouseId: _main, UnitCost: 11m));
        noLot.Lots.Should().BeEmpty();
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task A_movement_booked_before_W7_has_no_rows_and_replays_with_an_empty_breakdown()
    {
        var (product, lotId) = await SeedOneLotAsync();
        var c = Open();
        var old = new StockMovement(Guid.Empty, product, 2m, MovementType.ProductionIssue, null, null, "OLD-1#1")
            .Describe(_main, lotId, 10m, -20m, -2m);
        await c.Uow.StockMovements.AddAsync(old, default);
        await c.Uow.SaveChangesAsync(default);

        var replay = await RunAsync(Open(), new(MovementType.ProductionIssue, product, 2m, "OLD-1#1", LotId: lotId));

        replay.Replayed.Should().BeTrue();
        replay.LotId.Should().Be(lotId, "the stored LotId is still there");
        replay.Lots.Should().BeEmpty();
    }

    [Fact]
    public async Task The_rows_are_stamped_with_the_tenant_and_the_history_and_the_answer_carry_them()
    {
        var (product, lots) = await SeedAsync(ValuationMethod.FIFO, ("L-A", 30m, 10m, 10), ("L-B", 50m, 12m, 20));
        var reservation = await ReserveAsync(product, 40m);
        await RunAsync(Open(), new(MovementType.ProductionIssue, product, 40m, "MO-5#1", ReservationId: reservation));

        var rows = await Open().Db.StockMovementLots.IgnoreQueryFilters().ToListAsync();
        rows.Should().HaveCount(2).And.OnlyContain(l => l.TenantId == _tenant);

        var history = await new GetMovementHistoryQueryHandler(Open().Uow.StockMovements)
            .Handle(new GetMovementHistoryQuery { ProductId = product, Take = 10 }, default);
        history.Items.Single(i => i.MovementType == "ProductionIssue").Lots.Select(l => l.LotId).Should().BeEquivalentTo(new[] { lots["L-A"], lots["L-B"] });

        var json = JsonSerializer.Serialize(new RecordStockMovementResponse
        {
            MovementType = "ProductionIssue", Lots = [new MovementLotDto { LotId = lots["L-A"], LotNumber = "L-A", Quantity = 30m, UnitCost = 10m, ParentLotId = null }]
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Should().Contain("\"lots\":[{").And.Contain("\"lotNumber\":\"L-A\"").And.Contain("\"parentLotId\":null").And.Contain("\"unitCost\":10");
    }

    // ── a lot on quality hold (rework) ─────────────────────────────────────────────────────────────

    private async Task<(Guid Product, Guid HeldLot)> SeedHeldLotAsync()
    {
        var (product, _) = await SeedOneLotAsync();
        var made = await RunAsync(Open(), new(MovementType.ProductionReceipt, product, 6m, "FR-9#1", WarehouseId: _main, LotNumber: "FG-H", UnitCost: 10m, InitialLotStatus: LotStatus.QcHold));
        return (product, made.LotId!.Value);
    }

    [Fact]
    public async Task A_held_lot_is_issued_only_with_the_flag_the_lot_and_a_reason()
    {
        var (product, held) = await SeedHeldLotAsync();

        var plain = () => RunAsync(Open(), new(MovementType.ProductionIssue, product, 2m, "RW-1#1", LotId: held));
        await ExpectAsync(plain, "WH_LOT_NOT_USABLE");

        var noReason = () => RunAsync(Open(), new(MovementType.ProductionIssue, product, 2m, "RW-1#1", LotId: held, AllowHeldLot: true));
        await ExpectAsync(noReason, "WH_REASON_REQUIRED");

        var noLot = () => RunAsync(Open(), new(MovementType.ProductionIssue, product, 2m, "RW-1#1", AllowHeldLot: true, Reason: "Rework RW-1"));
        await ExpectAsync(noLot, "WH_LOT_REQUIRED");

        var ok = await RunAsync(Open(), new(MovementType.ProductionIssue, product, 2m, "RW-1#1", LotId: held, AllowHeldLot: true, Reason: "Rework RW-1"));

        ok.StockDelta.Should().Be(-2m);
        ok.ValueDelta.Should().Be(-20m);
        ok.Movement.Reason.Should().Be("Rework RW-1", "the audit trail");
        ok.Lots.Should().ContainSingle().Which.LotId.Should().Be(held);
        var lot = await LotAsync(held);
        lot.Should().Match<InventoryLot>(l => l.RemainingQuantity == 4m && l.Status == LotStatus.QcHold, "the lot stays on hold");
        await AssertParityAsync(product);
    }

    [Theory]
    [InlineData(MovementType.ProductionReturn)]
    [InlineData(MovementType.ProductionScrap)]
    [InlineData(MovementType.ProductionReceipt)]
    [InlineData(MovementType.SubcontractTransfer)]
    [InlineData(MovementType.QcRelease)]
    public async Task The_flag_is_refused_on_every_other_movement_type(MovementType type)
    {
        var (product, held) = await SeedHeldLotAsync();

        var act = () => RunAsync(Open(), new(type, product, 1m, "RW-2#1", WarehouseId: _main, ToWarehouseId: _north, LotId: held, LotNumber: "X", AllowHeldLot: true, Reason: "r"));

        await ExpectAsync(act, "WH_MOVEMENT_FLAG");
        (await LotAsync(held)).RemainingQuantity.Should().Be(6m);
    }

    [Fact]
    public async Task A_blocked_or_expired_lot_is_still_refused_with_the_flag_and_the_flag_is_harmless_on_an_available_lot()
    {
        var (product, held) = await SeedHeldLotAsync();
        var c = Open();
        await c.Ledger.SetLotStatusAsync(await c.Db.InventoryLots.SingleAsync(l => l.LotId == held), LotStatus.Blocked, default);
        await c.Uow.SaveChangesAsync(default);
        var blocked = () => RunAsync(Open(), new(MovementType.ProductionIssue, product, 1m, "RW-3#1", LotId: held, AllowHeldLot: true, Reason: "r"));
        await ExpectAsync(blocked, "WH_LOT_NOT_USABLE");

        var expired = await RunAsync(Open(), new(MovementType.ProductionReceipt, product, 3m, "RW-3#2", WarehouseId: _main, LotNumber: "FG-E",
            ExpiryDate: DateTime.UtcNow.AddDays(-1), UnitCost: 10m, InitialLotStatus: LotStatus.QcHold));
        var expiredAct = () => RunAsync(Open(), new(MovementType.ProductionIssue, product, 1m, "RW-3#3", LotId: expired.LotId, AllowHeldLot: true, Reason: "r"));
        await ExpectAsync(expiredAct, "WH_LOT_EXPIRED");

        var (product2, available) = await SeedOneLotAsync("L-OK");
        var fine = await RunAsync(Open(), new(MovementType.ProductionIssue, product2, 1m, "RW-3#4", LotId: available, AllowHeldLot: true, Reason: "r"));
        fine.StockDelta.Should().Be(-1m);
    }

    [Fact]
    public async Task The_internal_command_passes_the_flag_on_and_the_user_command_default_is_off()
    {
        RecordStockMovementCommand? sent = null;
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<RecordStockMovementCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<RecordStockMovementResponse>, CancellationToken>((cmd, _) => sent = (RecordStockMovementCommand)cmd)
            .ReturnsAsync(new RecordStockMovementResponse { MovementId = Guid.NewGuid(), MovementType = "ProductionIssue" });

        await new InternalProductionMovementCommandHandler(mediator.Object).Handle(new InternalProductionMovementCommand
        {
            MovementType = "ProductionIssue", ProductId = Guid.NewGuid(), Quantity = 1, IdempotencyKey = "RW-4#1", LotId = Guid.NewGuid(), AllowHeldLot = true, Reason = "Rework"
        }, default);

        sent!.AllowHeldLot.Should().BeTrue();
        new RecordStockMovementCommand { MovementType = "ProductionIssue" }.AllowHeldLot.Should().BeFalse();
    }
}

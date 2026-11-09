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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using InventoryEntity = ERP.WH.Domain.Aggregates.Inventory.Inventory;

namespace ERP.WH.UnitTests.Ledger;

/// <summary>
/// W4: reservations by source (production order) held in one warehouse, partial consume, release of the remainder, first-expiry-first lot
/// suggestion, and the fix that a manual sale / production issue no longer eats someone else's reservation. Parity after every scenario.
/// </summary>
public class ReservationBySourceTests
{
    private sealed class FixedTenant(Guid id) : ITenantProvider
    {
        public Guid? CurrentTenantId => id;
    }

    private readonly ServiceProvider _sp;
    private readonly Guid _a;
    private readonly Guid _b;

    public ReservationBySourceTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantProvider>(new FixedTenant(Guid.NewGuid()));
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
        services.AddSingleton<ERP.Shared.Infrastructure.Caching.ICacheService>(new Moq.Mock<ERP.Shared.Infrastructure.Caching.ICacheService>().Object);
        _sp = services.BuildServiceProvider();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
        var a = new Warehouse("MAIN", "A");
        var b = new Warehouse("NORTH", "B");
        db.Warehouses.AddRange(a, b);
        db.SaveChanges();
        _a = a.Id;
        _b = b.Id;
    }

    private sealed record Ctx(WarehouseDbContext Db, IWarehouseUnitOfWork Uow, IStockLedger Ledger, IProductionMovementService Production, ReserveForSourceCommandHandler Reserve);

    private Ctx Open()
    {
        var scope = _sp.CreateScope();
        var p = scope.ServiceProvider;
        var uow = p.GetRequiredService<IWarehouseUnitOfWork>();
        var ledger = p.GetRequiredService<IStockLedger>();
        return new Ctx(p.GetRequiredService<WarehouseDbContext>(), uow, ledger, p.GetRequiredService<IProductionMovementService>(),
            new ReserveForSourceCommandHandler(uow, ledger, p.GetRequiredService<ERP.Shared.Infrastructure.Caching.ICacheService>()));
    }

    /// <summary>An item with lots LB (A, expires in 30 days, 4), LA (A, in 90 days, 6), LC (B, in 10 days, 5) and 3 lot-less in A.</summary>
    private async Task<(Guid Product, Guid LA, Guid LB, Guid LC)> SeedAsync()
    {
        var c = Open();
        var item = new InventoryEntity(Guid.NewGuid(), "Paint", 0, 0);
        item.SetValuationMethod(ValuationMethod.Average);
        await c.Uow.Inventories.AddAsync(item, default);
        var la = await c.Ledger.ReceiveAsync(item, 6m, 10m, new StockTarget(_a, null), new NewLot("LA", 10m, DateTime.UtcNow.AddDays(90), null), default);
        var lb = await c.Ledger.ReceiveAsync(item, 4m, 10m, new StockTarget(_a, null), new NewLot("LB", 10m, DateTime.UtcNow.AddDays(30), null), default);
        var lc = await c.Ledger.ReceiveAsync(item, 5m, 10m, new StockTarget(_b, null), new NewLot("LC", 10m, DateTime.UtcNow.AddDays(10), null), default);
        await c.Ledger.ReceiveAsync(item, 3m, 10m, new StockTarget(_a, null), null, default);
        await c.Uow.SaveChangesAsync(default);
        return (item.ProductId, la!.LotId, lb!.LotId, lc!.LotId);
    }

    private async Task AssertParityAsync(Guid productId)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
        var inventory = await db.Inventories.SingleAsync(i => i.ProductId == productId);
        var balances = await db.StockBalances.Where(b => b.ProductId == productId).ToListAsync();
        balances.Sum(b => b.Quantity).Should().Be(inventory.TotalQuantity, "the balances add up to the item total");
        balances.Sum(b => b.ReservedQuantity).Should().Be(inventory.ReservedQuantity, "reserved on the balances equals reserved on the item");
        foreach (var lot in await db.InventoryLots.Where(l => l.ProductId == productId).ToListAsync())
        {
            balances.Where(b => b.LotId == lot.LotId).Sum(b => b.Quantity).Should().Be(lot.RemainingQuantity, $"lot {lot.LotNumber} equals its balance");
            balances.Where(b => b.LotId == lot.LotId).Sum(b => b.ReservedQuantity).Should().Be(lot.ReservedQuantity, $"lot {lot.LotNumber} reserved equals its balance");
        }
    }

    private static ReserveForSourceCommand Order(Guid source, Guid product, decimal qty, Guid warehouse, Guid? lot = null, Guid? line = null) => new()
    {
        SourceId = source,
        SourceReference = "MO-1",
        WarehouseId = warehouse,
        Lines = [new SourceReservationLineDto { ProductId = product, Quantity = qty, LotId = lot, SourceLineId = line }]
    };

    [Fact]
    public async Task A_production_reservation_takes_the_lots_of_its_warehouse_first_expiry_first_and_never_another_warehouse()
    {
        var (product, la, lb, lc) = await SeedAsync();
        var result = await Open().Reserve.Handle(Order(Guid.NewGuid(), product, 8m, _a), default);

        var lots = result.Lines.Single().Lots;
        lots.Select(l => (l.LotNumber, l.Quantity)).Should().Equal(("LB", 4m), ("LA", 4m));
        var db = Open().Db;
        (await db.InventoryLots.SingleAsync(l => l.LotId == lc)).ReservedQuantity.Should().Be(0m, "LC sits in the other warehouse");
        (await db.Reservations.SingleAsync()).Should().Match<Reservation>(r => r.SourceType == ReservationSource.ProductionOrder && r.HoldUntil.Year == 9999);
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task What_the_lots_do_not_cover_is_held_on_the_lot_less_stock_of_the_warehouse()
    {
        var (product, _, _, _) = await SeedAsync();
        var result = await Open().Reserve.Handle(Order(Guid.NewGuid(), product, 12m, _a), default);

        result.Lines.Single().Lots.Sum(l => l.Quantity).Should().Be(10m);
        var pool = await Open().Db.StockBalances.SingleAsync(b => b.WarehouseId == _a && b.LotId == null);
        pool.ReservedQuantity.Should().Be(2m);
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task A_warehouse_that_cannot_cover_the_quantity_holds_nothing_even_when_another_warehouse_has_stock()
    {
        var (product, _, _, _) = await SeedAsync();
        // A holds 13 (6 + 4 + 3), B holds 5: asking 15 in A fails although the item total is 18
        var act = () => Open().Reserve.Handle(Order(Guid.NewGuid(), product, 15m, _a), default);
        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Message.Should().Contain("Not enough stock");

        var db = Open().Db;
        (await db.Reservations.CountAsync()).Should().Be(0);
        (await db.Inventories.SingleAsync()).ReservedQuantity.Should().Be(0m);
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task A_held_expired_or_blocked_lot_is_never_taken()
    {
        var (product, la, lb, _) = await SeedAsync();
        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
            (await db.InventoryLots.SingleAsync(l => l.LotId == lb)).SetStatus(LotStatus.QcHold);
            await db.SaveChangesAsync();
        }

        var result = await Open().Reserve.Handle(Order(Guid.NewGuid(), product, 6m, _a), default);
        result.Lines.Single().Lots.Should().ContainSingle(l => l.LotNumber == "LA", "LB is on hold although it expires first");
        result.Lines.Single().Lots.Should().NotContain(l => l.LotNumber == "LB");
    }

    [Fact]
    public async Task A_pinned_lot_is_the_only_one_used_and_must_be_available_there()
    {
        var (product, la, _, lc) = await SeedAsync();
        var pinned = await Open().Reserve.Handle(Order(Guid.NewGuid(), product, 2m, _a, lot: la), default);
        pinned.Lines.Single().Lots.Should().ContainSingle(l => l.LotNumber == "LA" && l.Quantity == 2m);

        var wrongWarehouse = () => Open().Reserve.Handle(Order(Guid.NewGuid(), product, 1m, _a, lot: lc), default);
        await wrongWarehouse.Should().ThrowAsync<BusinessRuleException>();
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task Asking_again_for_the_same_source_answers_the_first_reservation()
    {
        var (product, _, _, _) = await SeedAsync();
        var source = Guid.NewGuid();
        var first = await Open().Reserve.Handle(Order(source, product, 3m, _a), default);
        var second = await Open().Reserve.Handle(Order(source, product, 3m, _a), default);

        second.Replayed.Should().BeTrue();
        second.ReservationId.Should().Be(first.ReservationId);
        (await Open().Db.Inventories.SingleAsync()).ReservedQuantity.Should().Be(3m);
    }

    [Fact]
    public async Task A_partial_consume_takes_stock_and_reservation_together_and_the_reservation_stays_open()
    {
        var (product, la, lb, _) = await SeedAsync();
        var source = Guid.NewGuid();
        var line = Guid.NewGuid();
        var reservation = await Open().Reserve.Handle(Order(source, product, 8m, _a, line: line), default);

        var c = Open();
        var issued = await c.Production.RecordAsync(new(MovementType.ProductionIssue, product, 3m, "MO-1#1", ReservationId: reservation.ReservationId, SourceLineId: line), default);
        await c.Uow.SaveChangesAsync(default);

        issued.StockDelta.Should().Be(-3m);
        issued.UnitCost.Should().Be(10m);
        issued.ValueDelta.Should().Be(-30m);
        var db = Open().Db;
        var res = await db.Reservations.SingleAsync();
        res.Status.Should().Be(ReservationStatus.Reserved);
        res.ReservedItems.Single().Should().Match<ReservedItem>(i => i.ConsumedQuantity == 3m && i.RemainingQuantity == 5m);
        res.ReservedItems.Single().LotAllocations.Sum(a => a.Consumed).Should().Be(3m);
        (await db.InventoryLots.SingleAsync(l => l.LotId == lb)).RemainingQuantity.Should().Be(1m, "LB (first expiry) gave 3 of its 4");
        (await db.Inventories.SingleAsync()).TotalQuantity.Should().Be(15m);
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task Consuming_the_rest_fulfils_the_reservation_and_more_than_that_is_refused()
    {
        var (product, _, _, _) = await SeedAsync();
        var source = Guid.NewGuid();
        var reservation = await Open().Reserve.Handle(Order(source, product, 5m, _a), default);

        var c1 = Open();
        await c1.Production.RecordAsync(new(MovementType.ProductionIssue, product, 2m, "MO-2#1", ReservationId: reservation.ReservationId), default);
        await c1.Uow.SaveChangesAsync(default);

        var tooMuch = () => Open().Production.RecordAsync(new(MovementType.ProductionIssue, product, 4m, "MO-2#2", ReservationId: reservation.ReservationId), default);
        (await tooMuch.Should().ThrowAsync<BusinessRuleException>()).Which.Message.Should().Contain("left to take");

        var c2 = Open();
        await c2.Production.RecordAsync(new(MovementType.ProductionIssue, product, 3m, "MO-2#3", ReservationId: reservation.ReservationId), default);
        await c2.Uow.SaveChangesAsync(default);

        var db = Open().Db;
        (await db.Reservations.SingleAsync()).Status.Should().Be(ReservationStatus.Fulfilled);
        (await db.Inventories.SingleAsync()).ReservedQuantity.Should().Be(0m);
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task Releasing_after_a_partial_consume_frees_only_the_remainder()
    {
        var (product, _, _, _) = await SeedAsync();
        var reservation = await Open().Reserve.Handle(Order(Guid.NewGuid(), product, 8m, _a), default);
        var c = Open();
        await c.Production.RecordAsync(new(MovementType.ProductionIssue, product, 3m, "MO-3#1", ReservationId: reservation.ReservationId), default);
        await c.Uow.SaveChangesAsync(default);

        var c2 = Open();
        var stored = await c2.Uow.Reservations.GetByIdAsync(reservation.ReservationId, default);
        await c2.Ledger.ReleaseAsync(stored!, default);
        stored!.Release("changed plan");
        await c2.Uow.Reservations.UpdateAsync(stored, default);
        await c2.Uow.SaveChangesAsync(default);

        var db = Open().Db;
        (await db.Inventories.SingleAsync()).Should().Match<InventoryEntity>(i => i.ReservedQuantity == 0m && i.TotalQuantity == 15m);
        (await db.InventoryLots.AnyAsync(l => l.ReservedQuantity != 0)).Should().BeFalse();
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task A_manual_sale_cannot_take_reserved_stock_and_does_not_lower_the_reservation()
    {
        var (product, _, _, _) = await SeedAsync();
        await Open().Reserve.Handle(Order(Guid.NewGuid(), product, 10m, _a), default);

        // 18 in total, 10 reserved: 8 are free
        var tooMuch = () => Open().Production.RecordAsync(new(MovementType.ProductionIssue, product, 9m, "MO-4#1", WarehouseId: _b), default);
        await tooMuch.Should().ThrowAsync<InvalidOperationException>();

        var c = Open();
        await c.Production.RecordAsync(new(MovementType.ProductionIssue, product, 5m, "MO-4#2", LotId: (await c.Db.InventoryLots.SingleAsync(l => l.LotNumber == "LC")).LotId), default);
        await c.Uow.SaveChangesAsync(default);
        (await Open().Db.Inventories.SingleAsync()).ReservedQuantity.Should().Be(10m, "the reservation of the order is untouched");
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task A_sale_of_free_lot_less_stock_leaves_the_reservation_on_the_balance_alone()
    {
        var c = Open();
        var item = new InventoryEntity(Guid.NewGuid(), "Screws", 0, 0);
        await c.Uow.Inventories.AddAsync(item, default);
        await c.Ledger.ReceiveAsync(item, 10m, 2m, new StockTarget(_a, null), null, default);
        await c.Uow.SaveChangesAsync(default);
        await Open().Reserve.Handle(Order(Guid.NewGuid(), item.ProductId, 8m, _a), default);

        var c2 = Open();
        await c2.Production.RecordAsync(new(MovementType.ProductionIssue, item.ProductId, 2m, "MO-9#1", WarehouseId: _a), default);
        await c2.Uow.SaveChangesAsync(default);

        var db = Open().Db;
        (await db.Inventories.SingleAsync()).Should().Match<InventoryEntity>(i => i.ReservedQuantity == 8m && i.TotalQuantity == 8m);
        (await db.StockBalances.SingleAsync()).Should().Match<StockBalance>(b => b.ReservedQuantity == 8m && b.Quantity == 8m);
        await AssertParityAsync(item.ProductId);
    }

    [Fact]
    public async Task The_sales_reserve_and_ship_path_still_works_on_the_new_ledger()
    {
        var (product, _, _, _) = await SeedAsync();
        var c = Open();
        var inventory = await c.Db.Inventories.SingleAsync();
        var held = await c.Ledger.ReserveAsync(inventory, 7m, new StockTarget(null, null), default);
        var item = new ReservedItem { ProductId = product, Quantity = 7m, WarehouseLocation = held.Location, LotAllocations = held.Allocations.ToList() };
        await c.Uow.SaveChangesAsync(default);

        // earliest expiry over all warehouses: LC (B, 10 days) 5, then LB (A, 30 days) 2
        item.LotAllocations.Select(a => a.Quantity).Should().Equal(5m, 2m);

        var c2 = Open();
        var inv2 = await c2.Db.Inventories.SingleAsync();
        var consumed = await c2.Ledger.ConsumeAsync(inv2, item, default);
        await c2.Uow.SaveChangesAsync(default);
        consumed.Sum(x => x.Quantity).Should().Be(7m);
        inv2.TotalQuantity.Should().Be(11m);
        inv2.ReservedQuantity.Should().Be(0m);
        item.ConsumedQuantity.Should().Be(7m);
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task The_suggestion_lists_lots_first_expiry_first_for_a_warehouse_and_the_shortfall()
    {
        var (product, _, _, _) = await SeedAsync();
        var c = Open();
        var handler = new SuggestLotsQueryHandler(c.Uow);

        var inA = await handler.Handle(new SuggestLotsQuery { ProductId = product, Quantity = 12m, WarehouseId = _a }, default);
        inA.Lots.Select(l => (l.LotNumber, l.Take)).Should().Equal(("LB", 4m), ("LA", 6m));
        inA.Shortfall.Should().Be(2m);
        inA.LotLessAvailable.Should().Be(3m, "the 3 lot-less units of A can cover the shortfall");

        var everywhere = await handler.Handle(new SuggestLotsQuery { ProductId = product, Quantity = 7m }, default);
        everywhere.Lots.Select(l => l.LotNumber).Should().Equal("LC", "LB");
    }

    [Fact]
    public void The_lot_picker_skips_hold_blocked_expired_and_full_lots_and_puts_no_expiry_last()
    {
        var now = DateTime.UtcNow;
        var held = new InventoryLot(Guid.NewGuid(), "H", 5m, 1m, now.AddDays(1));
        held.SetStatus(LotStatus.QcHold);
        var blocked = new InventoryLot(Guid.NewGuid(), "B", 5m, 1m, now.AddDays(2));
        blocked.SetStatus(LotStatus.Blocked);
        var expired = new InventoryLot(Guid.NewGuid(), "E", 5m, 1m, now.AddDays(-1));
        var full = new InventoryLot(Guid.NewGuid(), "F", 5m, 1m, now.AddDays(3));
        full.Reserve(5m);
        var never = new InventoryLot(Guid.NewGuid(), "N", 5m, 1m, null);
        var soon = new InventoryLot(Guid.NewGuid(), "S", 5m, 1m, now.AddDays(5));

        LotPicker.Candidates([held, blocked, expired, full, never, soon], null, false, now).Select(l => l.LotNumber).Should().Equal("S", "N");
    }
}

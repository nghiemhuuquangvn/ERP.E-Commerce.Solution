using ERP.Shared.Infrastructure.Data;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Commands.Locations;
using ERP.WH.Application.Commands.Stocktakes;
using ERP.WH.Application.Commands.Transfers;
using ERP.WH.Application.Interfaces;
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
/// W2: the stock ledger on a real <see cref="WarehouseDbContext"/> (EF in-memory) with the real repositories. After every scenario the item total
/// must equal the sum of its warehouse balances and each lot its balance - that is the whole point of one write path.
/// </summary>
public class StockLedgerTests
{
    private sealed class FixedTenant(Guid id) : ITenantProvider
    {
        public Guid? CurrentTenantId => id;
    }

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly ServiceProvider _sp;
    private readonly Guid _main;
    private readonly Guid _north;

    public StockLedgerTests()
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

    private sealed record Ctx(WarehouseDbContext Db, IWarehouseUnitOfWork Uow, IStockLedger Ledger);

    private Ctx Open()
    {
        var scope = _sp.CreateScope();
        return new Ctx(scope.ServiceProvider.GetRequiredService<WarehouseDbContext>(), scope.ServiceProvider.GetRequiredService<IWarehouseUnitOfWork>(), scope.ServiceProvider.GetRequiredService<IStockLedger>());
    }

    private static async Task<InventoryEntity> NewItemAsync(Ctx c, string name = "Paint")
    {
        var inventory = new InventoryEntity(Guid.NewGuid(), name, 0, 0);
        await c.Uow.Inventories.AddAsync(inventory, default);
        return inventory;
    }

    private async Task AssertParityAsync(Guid productId)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
        var inventory = await db.Inventories.SingleAsync(i => i.ProductId == productId);
        var balances = await db.StockBalances.Where(b => b.ProductId == productId).ToListAsync();
        balances.Sum(b => b.Quantity).Should().Be(inventory.TotalQuantity, "the balances add up to the item total");
        foreach (var lot in await db.InventoryLots.Where(l => l.ProductId == productId).ToListAsync())
            balances.Where(b => b.LotId == lot.LotId).Sum(b => b.Quantity).Should().Be(lot.RemainingQuantity, $"lot {lot.LotNumber} equals its balance");
    }

    [Fact]
    public async Task A_lot_less_receipt_lands_in_the_default_warehouse()
    {
        // MAIN is picked by the resolver when no default is configured
        var c = Open();
        var item = await NewItemAsync(c);
        var target = await c.Ledger.TargetAsync(null, null, default);
        target.WarehouseId.Should().Be(_main);

        await c.Ledger.ReceiveAsync(item, 2.5m, 10m, target, null, default);
        await c.Uow.SaveChangesAsync(default);

        (await c.Db.StockBalances.SingleAsync()).Should().Match<StockBalance>(b => b.WarehouseId == _main && b.LotId == null && b.Quantity == 2.5m);
        await AssertParityAsync(item.ProductId);
    }

    [Fact]
    public async Task A_named_warehouse_and_a_lot_get_their_own_balance()
    {
        var c = Open();
        var item = await NewItemAsync(c);
        var lot = await c.Ledger.ReceiveAsync(item, 4m, 8m, new StockTarget(_north, null), new NewLot("L-1", 8m, DateTime.UtcNow.AddDays(30), null), default);
        await c.Ledger.ReceiveAsync(item, 1m, 8m, new StockTarget(_main, null), null, default);
        await c.Uow.SaveChangesAsync(default);

        lot!.WarehouseId.Should().Be(_north);
        var balances = await c.Db.StockBalances.ToListAsync();
        balances.Should().HaveCount(2);
        balances.Single(b => b.LotId == lot.LotId).Should().Match<StockBalance>(b => b.WarehouseId == _north && b.Quantity == 4m);
        item.TotalQuantity.Should().Be(5m);
        await AssertParityAsync(item.ProductId);
    }

    [Fact]
    public async Task A_tenant_without_warehouses_uses_the_unassigned_bucket()
    {
        var lonely = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddSingleton<ITenantProvider>(new FixedTenant(lonely));
        services.AddDbContext<WarehouseDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IWarehouseResolver, WarehouseResolver>();
        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IWarehouseResolver>().DefaultAsync(default)).Should().BeNull();
    }

    [Fact]
    public async Task The_configured_default_warehouse_wins_over_MAIN()
    {
        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
            var settings = ERP.WH.Domain.Settings.TenantInventorySettings.CreateDefault(_tenant);
            settings.Update(10, false, 7, _north);
            db.TenantInventorySettings.Add(settings);
            await db.SaveChangesAsync();
        }

        var c = Open();
        (await c.Ledger.TargetAsync(null, null, default)).WarehouseId.Should().Be(_north);
        (await c.Ledger.TargetAsync(_main, null, default)).WarehouseId.Should().Be(_main, "a named warehouse always wins");
    }

    [Fact]
    public async Task A_bin_in_another_warehouse_decides_the_warehouse()
    {
        Guid binId;
        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
            var bin = new WarehouseLocation("N-01", "A", "1", "1", 100);
            bin.AssignWarehouse(_north);
            db.WarehouseLocations.Add(bin);
            await db.SaveChangesAsync();
            binId = bin.LocationId;
        }

        var c = Open();
        var target = await c.Ledger.TargetAsync(null, binId, default);
        target.WarehouseId.Should().Be(_north);

        var item = await NewItemAsync(c);
        await c.Ledger.ReceiveAsync(item, 3m, 5m, target, null, default);
        await c.Uow.SaveChangesAsync(default);
        (await c.Db.InventoryLocationStocks.SingleAsync()).Quantity.Should().Be(3m);
        (await c.Db.StockBalances.SingleAsync()).WarehouseId.Should().Be(_north);
        await AssertParityAsync(item.ProductId);
    }

    [Fact]
    public async Task Fifo_depletion_takes_the_oldest_lot_first_and_keeps_parity()
    {
        var c = Open();
        var item = await NewItemAsync(c);
        var target = new StockTarget(_main, null);
        var first = await c.Ledger.ReceiveAsync(item, 2m, 5m, target, new NewLot("OLD", 5m, null, null), default);
        await c.Uow.SaveChangesAsync(default);
        var second = await c.Ledger.ReceiveAsync(item, 3m, 6m, target, new NewLot("NEW", 6m, null, null), default);
        await c.Uow.SaveChangesAsync(default);
        // lot receipts carry the received date of "now": make the first one older
        typeof(InventoryLot).GetProperty(nameof(InventoryLot.ReceivedDate))!.SetValue(first, DateTime.UtcNow.AddDays(-5));
        await c.Uow.SaveChangesAsync(default);

        await c.Ledger.IssueAsync(item, 3.5m, target, IssuePolicy.Refuse, LotDepletion.Fifo, default);
        await c.Uow.SaveChangesAsync(default);

        first!.RemainingQuantity.Should().Be(0m);
        second!.RemainingQuantity.Should().Be(1.5m);
        item.TotalQuantity.Should().Be(1.5m);
        await AssertParityAsync(item.ProductId);
    }

    [Fact]
    public async Task A_point_of_sale_issue_may_go_negative_and_a_manual_sale_may_not()
    {
        var c = Open();
        var item = await NewItemAsync(c);
        var target = new StockTarget(_main, null);
        await c.Ledger.ReceiveAsync(item, 1m, 5m, target, null, default);
        await c.Uow.SaveChangesAsync(default);

        var refused = async () => await c.Ledger.IssueAsync(item, 2m, target, IssuePolicy.Refuse, LotDepletion.None, default);
        await refused.Should().ThrowAsync<InvalidOperationException>();

        await c.Ledger.IssueAsync(item, 2m, target, IssuePolicy.AllowNegative, LotDepletion.None, default);
        await c.Uow.SaveChangesAsync(default);
        item.TotalQuantity.Should().Be(-1m);
        await AssertParityAsync(item.ProductId);
    }

    [Fact]
    public async Task A_lot_less_issue_takes_from_the_other_warehouse_before_going_negative()
    {
        var c = Open();
        var item = await NewItemAsync(c);
        await c.Ledger.ReceiveAsync(item, 1m, 5m, new StockTarget(_main, null), null, default);
        await c.Ledger.ReceiveAsync(item, 4m, 5m, new StockTarget(_north, null), null, default);
        await c.Uow.SaveChangesAsync(default);

        await c.Ledger.IssueAsync(item, 3m, new StockTarget(_main, null), IssuePolicy.Refuse, LotDepletion.None, default);
        await c.Uow.SaveChangesAsync(default);

        var balances = await c.Db.StockBalances.ToListAsync();
        balances.Single(b => b.WarehouseId == _main).Quantity.Should().Be(0m);
        balances.Single(b => b.WarehouseId == _north).Quantity.Should().Be(2m);
        balances.Should().NotContain(b => b.Quantity < 0);
        await AssertParityAsync(item.ProductId);
    }

    [Fact]
    public async Task Reserve_splits_across_lots_first_expiry_first_skips_a_held_lot_and_releases_cleanly()
    {
        var c = Open();
        var item = await NewItemAsync(c);
        var target = new StockTarget(_main, null);
        var soon = await c.Ledger.ReceiveAsync(item, 1m, 5m, target, new NewLot("SOON", 5m, DateTime.UtcNow.AddDays(5), null), default);
        var later = await c.Ledger.ReceiveAsync(item, 5m, 5m, target, new NewLot("LATER", 5m, DateTime.UtcNow.AddDays(60), null), default);
        var held = await c.Ledger.ReceiveAsync(item, 9m, 5m, target, new NewLot("QC", 5m, DateTime.UtcNow.AddDays(1), null, null, LotSource.Production), default);
        held!.SetStatus(LotStatus.QcHold);
        await c.Uow.SaveChangesAsync(default);

        var reserved = await c.Ledger.ReserveAsync(item, 2.5m, target, default);
        await c.Uow.SaveChangesAsync(default);

        reserved.Allocations.Select(a => (a.LotId, a.Quantity)).Should().BeEquivalentTo(new[] { (soon!.LotId, 1m), (later!.LotId, 1.5m) });
        held.ReservedQuantity.Should().Be(0m, "a lot on quality hold is never allocated");
        item.ReservedQuantity.Should().Be(2.5m);
        (await c.Db.StockBalances.SingleAsync(b => b.LotId == later.LotId)).ReservedQuantity.Should().Be(1.5m);

        var reservation = new Reservation(Guid.NewGuid(),
            [new ReservedItem { ProductId = item.ProductId, Quantity = 2.5m, WarehouseLocation = reserved.Location, LotAllocations = reserved.Allocations.ToList() }], DateTime.UtcNow.AddHours(1));
        await c.Ledger.ReleaseAsync(reservation, default);
        await c.Uow.SaveChangesAsync(default);

        item.ReservedQuantity.Should().Be(0m);
        (await c.Db.StockBalances.Where(b => b.ProductId == item.ProductId).SumAsync(b => b.ReservedQuantity)).Should().Be(0m);
        await AssertParityAsync(item.ProductId);
    }

    [Fact]
    public async Task Consume_ships_the_reserved_lots_and_the_lot_less_rest()
    {
        var c = Open();
        var item = await NewItemAsync(c);
        var target = new StockTarget(_main, null);
        var lot = await c.Ledger.ReceiveAsync(item, 1m, 5m, target, new NewLot("L", 7m, null, null), default);
        await c.Ledger.ReceiveAsync(item, 4m, 5m, target, null, default);
        await c.Uow.SaveChangesAsync(default);

        var held = await c.Ledger.ReserveAsync(item, 3m, target, default);
        await c.Uow.SaveChangesAsync(default);
        var reservedItem = new ReservedItem { ProductId = item.ProductId, Quantity = 3m, WarehouseLocation = held.Location, LotAllocations = held.Allocations.ToList() };

        var consumed = await c.Ledger.ConsumeAsync(item, reservedItem, default);
        await c.Uow.SaveChangesAsync(default);

        consumed.Should().ContainSingle().Which.Should().Match<ConsumedLot>(x => x.Lot.LotId == lot!.LotId && x.Quantity == 1m);
        item.TotalQuantity.Should().Be(2m);
        item.ReservedQuantity.Should().Be(0m);
        (await c.Db.StockBalances.Where(b => b.ProductId == item.ProductId).SumAsync(b => b.ReservedQuantity)).Should().Be(0m);
        await AssertParityAsync(item.ProductId);
    }

    [Fact]
    public async Task A_transfer_between_bins_of_two_warehouses_moves_the_balance()
    {
        Guid fromBin, toBin;
        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
            var a = new WarehouseLocation("M-01", "A", "1", "1", 100);
            a.AssignWarehouse(_main);
            var b = new WarehouseLocation("N-01", "A", "1", "1", 100);
            b.AssignWarehouse(_north);
            db.WarehouseLocations.AddRange(a, b);
            await db.SaveChangesAsync();
            fromBin = a.LocationId;
            toBin = b.LocationId;
        }

        var c = Open();
        var item = await NewItemAsync(c);
        await c.Ledger.ReceiveAsync(item, 5m, 5m, await c.Ledger.TargetAsync(null, fromBin, default), null, default);
        await c.Uow.SaveChangesAsync(default);

        await c.Ledger.TransferAsync(item.ProductId, fromBin, toBin, 2m, default);
        await c.Uow.SaveChangesAsync(default);

        var balances = await c.Db.StockBalances.ToListAsync();
        balances.Single(x => x.WarehouseId == _main).Quantity.Should().Be(3m);
        balances.Single(x => x.WarehouseId == _north).Quantity.Should().Be(2m);
        item.TotalQuantity.Should().Be(5m, "a transfer never changes the item total");
        await AssertParityAsync(item.ProductId);
    }

    [Fact]
    public async Task An_adjustment_up_and_down_keeps_parity_with_fractions()
    {
        var c = Open();
        var item = await NewItemAsync(c);
        var target = new StockTarget(_main, null);
        await c.Ledger.AdjustAsync(item, 3.75m, target, default);
        await c.Ledger.AdjustAsync(item, -1.25m, target, default);
        await c.Uow.SaveChangesAsync(default);

        item.TotalQuantity.Should().Be(2.5m);
        (await c.Db.StockBalances.SingleAsync()).Quantity.Should().Be(2.5m);
        await AssertParityAsync(item.ProductId);
    }

    [Fact]
    public async Task The_drift_report_lists_an_item_whose_total_was_changed_behind_the_ledger()
    {
        var c = Open();
        var item = await NewItemAsync(c);
        await c.Ledger.ReceiveAsync(item, 2m, 5m, new StockTarget(_main, null), null, default);
        await c.Uow.SaveChangesAsync(default);
        (await c.Uow.StockBalances.FindDriftAsync(10, default)).Should().BeEmpty();

        item.AdjustStock(1m);   // the forbidden shortcut
        await c.Uow.SaveChangesAsync(default);

        (await c.Uow.StockBalances.FindDriftAsync(10, default)).Should().ContainSingle(d => d.Kind == "Quantity" && d.Expected == 3m && d.Actual == 2m);
    }
}

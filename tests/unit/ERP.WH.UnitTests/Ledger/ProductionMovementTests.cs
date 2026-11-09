using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
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
/// W3: the production movements (issue, return, receipt, scrap, quality release, subcontract transfer) on a real <see cref="WarehouseDbContext"/>:
/// effect on the item total / lot / balances, value and cost on the movement, idempotency by reference, and parity after every scenario.
/// </summary>
public class ProductionMovementTests
{
    private sealed class FixedTenant(Guid id) : ITenantProvider
    {
        public Guid? CurrentTenantId => id;
    }

    private readonly ServiceProvider _sp;
    private readonly Guid _main;
    private readonly Guid _north;

    public ProductionMovementTests()
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

    private sealed record Ctx(WarehouseDbContext Db, IWarehouseUnitOfWork Uow, IStockLedger Ledger, IProductionMovementService Production);

    private Ctx Open()
    {
        var scope = _sp.CreateScope();
        var p = scope.ServiceProvider;
        return new Ctx(p.GetRequiredService<WarehouseDbContext>(), p.GetRequiredService<IWarehouseUnitOfWork>(), p.GetRequiredService<IStockLedger>(), p.GetRequiredService<IProductionMovementService>());
    }

    /// <summary>An item holding 10 in lot L-1 at MAIN, cost 10, Average valuation.</summary>
    private async Task<(Guid ProductId, Guid LotId)> SeedItemAsync(ValuationMethod method = ValuationMethod.Average, string lotNumber = "L-1")
    {
        var c = Open();
        var item = new InventoryEntity(Guid.NewGuid(), "Paint", 0, 0);
        item.SetValuationMethod(method);
        await c.Uow.Inventories.AddAsync(item, default);
        var lot = await c.Ledger.ReceiveAsync(item, 10m, 10m, new StockTarget(_main, null), new NewLot(lotNumber, 10m, DateTime.UtcNow.AddDays(60), null), default);
        await c.Uow.SaveChangesAsync(default);
        return (item.ProductId, lot!.LotId);
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

    private static async Task<ProductionMovementResult> RunAsync(Ctx c, ProductionMovementRequest request)
    {
        var result = await c.Production.RecordAsync(request, default);
        await c.Uow.SaveChangesAsync(default);
        return result;
    }

    [Fact]
    public async Task Issue_from_a_lot_lowers_the_total_the_lot_and_the_balance_and_records_cost_and_value()
    {
        var (product, lotId) = await SeedItemAsync();
        var c = Open();

        var r = await RunAsync(c, new(MovementType.ProductionIssue, product, 2.5m, "MO-1#1", LotId: lotId));

        r.Replayed.Should().BeFalse();
        r.UnitCost.Should().Be(10m);
        r.ValueDelta.Should().Be(-25m);
        r.StockDelta.Should().Be(-2.5m);
        r.CurrentQuantity.Should().Be(7.5m);
        var m = await c.Db.StockMovements.SingleAsync(x => x.MovementType == MovementType.ProductionIssue);
        m.Should().Match<StockMovement>(x => x.WarehouseId == _main && x.LotId == lotId && x.UnitCost == 10m && x.ValueDelta == -25m && x.StockDelta == -2.5m && x.Quantity == 2.5m);
        (await c.Db.InventoryLots.SingleAsync()).RemainingQuantity.Should().Be(7.5m);
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task Fifo_valuation_issues_at_the_cost_of_the_lot()
    {
        var (product, lotId) = await SeedItemAsync(ValuationMethod.FIFO);
        var c = Open();
        // the item cost moves on (another receipt at 14), the lot still costs 10
        var inventory = await c.Db.Inventories.SingleAsync(i => i.ProductId == product);
        await c.Ledger.ReceiveAsync(inventory, 5m, 14m, new StockTarget(_main, null), null, default);
        await c.Uow.SaveChangesAsync(default);

        var r = await RunAsync(Open(), new(MovementType.ProductionIssue, product, 4m, "MO-2#1", LotId: lotId));

        r.UnitCost.Should().Be(10m);
        r.ValueDelta.Should().Be(-40m);
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task The_same_reference_twice_is_one_movement_and_the_first_result_comes_back()
    {
        var (product, lotId) = await SeedItemAsync();
        var first = await RunAsync(Open(), new(MovementType.ProductionIssue, product, 3m, "MO-3#1", LotId: lotId));
        var second = await RunAsync(Open(), new(MovementType.ProductionIssue, product, 3m, "MO-3#1", LotId: lotId));

        second.Replayed.Should().BeTrue();
        second.Movement.MovementId.Should().Be(first.Movement.MovementId);
        second.ValueDelta.Should().Be(first.ValueDelta);
        var c = Open();
        (await c.Db.StockMovements.CountAsync(x => x.MovementType == MovementType.ProductionIssue)).Should().Be(1);
        (await c.Db.Inventories.SingleAsync(i => i.ProductId == product)).TotalQuantity.Should().Be(7m, "the second call did not issue again");
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task Another_line_of_the_same_document_is_a_second_movement()
    {
        var (product, lotId) = await SeedItemAsync();
        await RunAsync(Open(), new(MovementType.ProductionIssue, product, 1m, "MO-4#1", LotId: lotId));
        var second = await RunAsync(Open(), new(MovementType.ProductionIssue, product, 1m, "MO-4#2", LotId: lotId));

        second.Replayed.Should().BeFalse();
        (await Open().Db.Inventories.SingleAsync(i => i.ProductId == product)).TotalQuantity.Should().Be(8m);
    }

    [Fact]
    public async Task A_reference_is_required_and_a_quantity_must_be_positive()
    {
        var (product, lotId) = await SeedItemAsync();
        var c = Open();

        var noReference = () => c.Production.RecordAsync(new(MovementType.ProductionIssue, product, 1m, " ", LotId: lotId), default);
        (await noReference.Should().ThrowAsync<BusinessRuleException>()).Which.Message.Should().Contain("reference");
        var noQuantity = () => c.Production.RecordAsync(new(MovementType.ProductionIssue, product, 0m, "MO-5#1", LotId: lotId), default);
        await noQuantity.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task A_receipt_into_a_lot_on_hold_is_not_allocated_until_it_is_released()
    {
        var (product, _) = await SeedItemAsync();
        var received = await RunAsync(Open(), new(MovementType.ProductionReceipt, product, 5m, "MO-6#1", WarehouseId: _main, LotNumber: "FG-1",
            ExpiryDate: DateTime.UtcNow.AddDays(10), UnitCost: 12m, InitialLotStatus: LotStatus.QcHold));

        received.StockDelta.Should().Be(5m);
        received.ValueDelta.Should().Be(60m);
        var c = Open();
        var held = await c.Db.InventoryLots.SingleAsync(l => l.LotNumber == "FG-1");
        held.Should().Match<InventoryLot>(l => l.Status == LotStatus.QcHold && l.Source == LotSource.Production && l.WarehouseId == _main);
        received.LotId.Should().Be(held.LotId);

        // reserve 12: the older Available lot (10) is taken first; the held lot gets nothing
        var inventory = await c.Db.Inventories.SingleAsync(i => i.ProductId == product);
        await c.Ledger.ReserveAsync(inventory, 12m, new StockTarget(_main, null), default);
        await c.Uow.SaveChangesAsync(default);
        (await Open().Db.InventoryLots.SingleAsync(l => l.LotNumber == "FG-1")).ReservedQuantity.Should().Be(0m, "a lot on hold is never allocated");

        var release = await RunAsync(Open(), new(MovementType.QcRelease, product, 0m, "QC-1#1", LotId: held.LotId));
        release.StockDelta.Should().Be(0m);
        release.ValueDelta.Should().Be(0m);
        release.Movement.Quantity.Should().Be(5m, "the release records what is left in the lot");
        (await Open().Db.InventoryLots.SingleAsync(l => l.LotNumber == "FG-1")).Status.Should().Be(LotStatus.Available);

        var again = () => Open().Production.RecordAsync(new(MovementType.QcRelease, product, 0m, "QC-1#2", LotId: held.LotId), default);
        await again.Should().ThrowAsync<BusinessRuleException>();
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task A_held_lot_cannot_be_issued_but_can_be_scrapped()
    {
        var (product, _) = await SeedItemAsync();
        var received = await RunAsync(Open(), new(MovementType.ProductionReceipt, product, 4m, "MO-7#1", WarehouseId: _main, LotNumber: "FG-2", UnitCost: 10m, InitialLotStatus: LotStatus.QcHold));

        var issue = () => Open().Production.RecordAsync(new(MovementType.ProductionIssue, product, 1m, "MO-7#2", LotId: received.LotId), default);
        await issue.Should().ThrowAsync<BusinessRuleException>();

        var scrap = await RunAsync(Open(), new(MovementType.ProductionScrap, product, 1m, "MO-7#3", LotId: received.LotId));
        scrap.StockDelta.Should().Be(-1m);
        scrap.ValueDelta.Should().Be(-10m);
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task A_return_goes_back_into_the_lot_and_re_averages_the_cost()
    {
        var (product, lotId) = await SeedItemAsync();
        await RunAsync(Open(), new(MovementType.ProductionIssue, product, 4m, "MO-8#1", LotId: lotId));

        var back = await RunAsync(Open(), new(MovementType.ProductionReturn, product, 4m, "MO-8#2", LotId: lotId, UnitCost: 12m));

        back.StockDelta.Should().Be(4m);
        back.ValueDelta.Should().Be(48m);
        var c = Open();
        (await c.Db.Inventories.SingleAsync(i => i.ProductId == product)).Cost.Should().Be(10.8m, "(6 x 10 + 4 x 12) / 10");
        (await c.Db.InventoryLots.SingleAsync()).RemainingQuantity.Should().Be(10m);
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task A_subcontract_transfer_moves_the_balance_and_keeps_the_total()
    {
        var item = new InventoryEntity(Guid.NewGuid(), "Cut part", 0, 0);
        var c = Open();
        await c.Uow.Inventories.AddAsync(item, default);
        await c.Ledger.ReceiveAsync(item, 8m, 5m, new StockTarget(_main, null), null, default);
        await c.Uow.SaveChangesAsync(default);

        var r = await RunAsync(Open(), new(MovementType.SubcontractTransfer, item.ProductId, 3m, "SC-1#1", WarehouseId: _main, ToWarehouseId: _north));

        r.StockDelta.Should().Be(0m);
        r.ValueDelta.Should().Be(0m);
        var balances = await Open().Db.StockBalances.Where(b => b.ProductId == item.ProductId).ToListAsync();
        balances.Single(b => b.WarehouseId == _main).Quantity.Should().Be(5m);
        balances.Single(b => b.WarehouseId == _north).Quantity.Should().Be(3m);
        (await Open().Db.Inventories.SingleAsync(i => i.ProductId == item.ProductId)).TotalQuantity.Should().Be(8m);
        await AssertParityAsync(item.ProductId);
    }

    [Fact]
    public async Task A_lot_moves_to_another_warehouse_only_as_a_whole()
    {
        var (product, lotId) = await SeedItemAsync();

        var partial = () => Open().Production.RecordAsync(new(MovementType.SubcontractTransfer, product, 4m, "SC-2#1", WarehouseId: _main, ToWarehouseId: _north, LotId: lotId), default);
        await partial.Should().ThrowAsync<BusinessRuleException>();

        await RunAsync(Open(), new(MovementType.SubcontractTransfer, product, 10m, "SC-2#2", WarehouseId: _main, ToWarehouseId: _north, LotId: lotId));
        (await Open().Db.InventoryLots.SingleAsync()).WarehouseId.Should().Be(_north);
        await AssertParityAsync(product);
    }

    [Fact]
    public async Task An_issue_beyond_the_stock_is_refused_unless_negative_stock_is_allowed()
    {
        var (product, lotId) = await SeedItemAsync();

        var refused = () => Open().Production.RecordAsync(new(MovementType.ProductionIssue, product, 11m, "MO-9#1", LotId: lotId), default);
        await refused.Should().ThrowAsync<Exception>();

        var c = Open();
        var inventory = await c.Db.Inventories.SingleAsync(i => i.ProductId == product);
        inventory.TotalQuantity.Should().Be(10m, "nothing was changed by the refusal");
    }

    [Fact]
    public async Task A_lot_less_issue_uses_the_pool_of_the_named_warehouse()
    {
        var item = new InventoryEntity(Guid.NewGuid(), "Screws", 0, 0);
        var c = Open();
        await c.Uow.Inventories.AddAsync(item, default);
        await c.Ledger.ReceiveAsync(item, 6m, 2m, new StockTarget(_main, null), null, default);
        await c.Ledger.ReceiveAsync(item, 2m, 2m, new StockTarget(_north, null), null, default);
        await c.Uow.SaveChangesAsync(default);

        var r = await RunAsync(Open(), new(MovementType.ProductionIssue, item.ProductId, 3m, "MO-10#1", WarehouseId: _north));

        r.WarehouseId.Should().Be(_north);
        (await Open().Db.Inventories.SingleAsync(i => i.ProductId == item.ProductId)).TotalQuantity.Should().Be(5m);
        await AssertParityAsync(item.ProductId);
    }
}

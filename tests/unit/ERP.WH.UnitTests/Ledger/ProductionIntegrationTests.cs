using ERP.Shared.Infrastructure.Caching;
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
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using InventoryEntity = ERP.WH.Domain.Aggregates.Inventory.Inventory;

namespace ERP.WH.UnitTests.Ledger;

/// <summary>
/// W5: what the internal stock API relies on - the idempotency key (replay vs conflict), the lot / serial flags passed by Manufacturing,
/// the key format, quality hold of a lot and an idempotent release (docs/business/Warehouse/ProductionIntegration.md).
/// </summary>
public class ProductionIntegrationTests
{
    private sealed class FixedTenant(Guid id) : ITenantProvider
    {
        public Guid? CurrentTenantId => id;
    }

    private readonly ServiceProvider _sp;
    private readonly Guid _main;
    private readonly Guid _north;

    public ProductionIntegrationTests()
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

    /// <summary>An item holding 10 in lot L-1 at MAIN (cost 10) and 4 lot-less at MAIN.</summary>
    private async Task<(Guid Product, Guid Lot)> SeedAsync()
    {
        var c = Open();
        var item = new InventoryEntity(Guid.NewGuid(), "Paint", 0, 0);
        item.SetValuationMethod(ValuationMethod.Average);
        await c.Uow.Inventories.AddAsync(item, default);
        var lot = await c.Ledger.ReceiveAsync(item, 10m, 10m, new StockTarget(_main, null), new NewLot("L-1", 10m, DateTime.UtcNow.AddDays(60), null), default);
        await c.Ledger.ReceiveAsync(item, 4m, 10m, new StockTarget(_main, null), null, default);
        await c.Uow.SaveChangesAsync(default);
        return (item.ProductId, lot!.LotId);
    }

    private static async Task<ProductionMovementResult> RunAsync(Ctx c, ProductionMovementRequest request)
    {
        var result = await c.Production.RecordAsync(request, default);
        await c.Uow.SaveChangesAsync(default);
        return result;
    }

    private async Task<decimal> TotalAsync(Guid product) => (await Open().Db.Inventories.SingleAsync(i => i.ProductId == product)).TotalQuantity;

    // ── idempotency key ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_same_key_with_the_same_payload_answers_the_first_result_and_changes_nothing()
    {
        var (product, lot) = await SeedAsync();
        var first = await RunAsync(Open(), new(MovementType.ProductionIssue, product, 3m, "MO-1#1", LotId: lot));

        var again = await RunAsync(Open(), new(MovementType.ProductionIssue, product, 3m, "MO-1#1", LotId: lot));

        again.Replayed.Should().BeTrue();
        again.Movement.MovementId.Should().Be(first.Movement.MovementId);
        again.ValueDelta.Should().Be(first.ValueDelta);
        (await TotalAsync(product)).Should().Be(11m);
        (await Open().Db.StockMovements.CountAsync(m => m.MovementType == MovementType.ProductionIssue)).Should().Be(1);
    }

    [Fact]
    public async Task The_same_key_with_another_quantity_is_a_conflict_that_carries_its_code_and_changes_nothing()
    {
        var (product, lot) = await SeedAsync();
        await RunAsync(Open(), new(MovementType.ProductionIssue, product, 3m, "MO-1#1", LotId: lot));

        var act = () => RunAsync(Open(), new(MovementType.ProductionIssue, product, 4m, "MO-1#1", LotId: lot));

        (await act.Should().ThrowAsync<ConcurrencyException>()).Which.ErrorCode.Should().Be("WH_IDEMPOTENCY_CONFLICT");
        (await TotalAsync(product)).Should().Be(11m);
    }

    [Fact]
    public async Task The_same_key_with_another_lot_or_warehouse_is_a_conflict()
    {
        var (product, lot) = await SeedAsync();
        await RunAsync(Open(), new(MovementType.ProductionIssue, product, 3m, "MO-1#1", LotId: lot));

        var otherWarehouse = () => RunAsync(Open(), new(MovementType.ProductionIssue, product, 3m, "MO-1#1", WarehouseId: _north, LotId: lot));

        await otherWarehouse.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task A_quality_release_replays_even_though_its_quantity_is_the_remainder()
    {
        var (product, lot) = await SeedAsync();
        await RunAsync(Open(), new(MovementType.ProductionReceipt, product, 5m, "MO-2#FG1", WarehouseId: _north, LotNumber: "FG-1", UnitCost: 12m, InitialLotStatus: LotStatus.QcHold));
        var held = (await Open().Db.InventoryLots.SingleAsync(l => l.LotNumber == "FG-1")).LotId;

        var first = await RunAsync(Open(), new(MovementType.QcRelease, product, 0m, "MO-2#QC1", LotId: held));
        var again = await RunAsync(Open(), new(MovementType.QcRelease, product, 0m, "MO-2#QC1", LotId: held));

        first.Replayed.Should().BeFalse();
        again.Replayed.Should().BeTrue();
        (await Open().Db.InventoryLots.SingleAsync(l => l.LotId == held)).Status.Should().Be(LotStatus.Available);
        lot.Should().NotBeEmpty();
    }

    // ── key format / type, in the internal command ──────────────────────────────────────────────────

    [Theory]
    [InlineData("MO-1")]
    [InlineData("#1")]
    [InlineData("MO-1#")]
    [InlineData("")]
    public async Task A_key_that_is_not_document_hash_line_is_refused(string key)
    {
        var mediator = new Mock<IMediator>();
        var handler = new InternalProductionMovementCommandHandler(mediator.Object);

        var act = () => handler.Handle(new InternalProductionMovementCommand { MovementType = "ProductionIssue", ProductId = Guid.NewGuid(), Quantity = 1, IdempotencyKey = key }, default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("WH_REFERENCE_FORMAT");
        mediator.Verify(m => m.Send(It.IsAny<RecordStockMovementCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Sold")]
    [InlineData("Received")]
    [InlineData("Nonsense")]
    public async Task A_legacy_or_unknown_type_is_refused_on_the_internal_route(string type)
    {
        var handler = new InternalProductionMovementCommandHandler(new Mock<IMediator>().Object);

        var act = () => handler.Handle(new InternalProductionMovementCommand { MovementType = type, ProductId = Guid.NewGuid(), Quantity = 1, IdempotencyKey = "MO-1#1" }, default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("WH_MOVEMENT_TYPE");
    }

    [Fact]
    public async Task A_valid_internal_movement_is_passed_on_with_the_key_as_its_reference_and_the_flags()
    {
        var mediator = new Mock<IMediator>();
        RecordStockMovementCommand? sent = null;
        mediator.Setup(m => m.Send(It.IsAny<RecordStockMovementCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<RecordStockMovementResponse>, CancellationToken>((c, _) => sent = (RecordStockMovementCommand)c)
            .ReturnsAsync(new RecordStockMovementResponse { MovementId = Guid.NewGuid(), MovementType = "ProductionScrap" });
        var handler = new InternalProductionMovementCommandHandler(mediator.Object);

        await handler.Handle(new InternalProductionMovementCommand { MovementType = "productionscrap", ProductId = Guid.NewGuid(), Quantity = 2, IdempotencyKey = " MO-9#4 ", RequiresLot = true }, default);

        sent.Should().NotBeNull();
        sent!.Reference.Should().Be("MO-9#4");
        sent.MovementType.Should().Be("ProductionScrap");
        sent.RequiresLot.Should().BeTrue();
    }

    // ── serial / lot flags ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(MovementType.ProductionIssue)]
    [InlineData(MovementType.ProductionReturn)]
    [InlineData(MovementType.ProductionReceipt)]
    [InlineData(MovementType.ProductionScrap)]
    [InlineData(MovementType.SubcontractTransfer)]
    public async Task A_serial_controlled_item_is_refused_for_every_stock_moving_type_and_nothing_changes(MovementType type)
    {
        var (product, lot) = await SeedAsync();

        var act = () => RunAsync(Open(), new(type, product, 1m, "MO-3#1", LotId: lot, LotNumber: "X", ToWarehouseId: _north, RequiresSerial: true));

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("WH_SERIAL_NOT_SUPPORTED");
        (await TotalAsync(product)).Should().Be(14m);
        (await Open().Db.StockMovements.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_lot_controlled_receipt_needs_a_lot_number()
    {
        var (product, _) = await SeedAsync();

        var without = () => RunAsync(Open(), new(MovementType.ProductionReceipt, product, 2m, "MO-4#1", UnitCost: 5m, RequiresLot: true));
        (await without.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("WH_LOT_REQUIRED");

        var with = await RunAsync(Open(), new(MovementType.ProductionReceipt, product, 2m, "MO-4#2", LotNumber: "FG-7", UnitCost: 5m, RequiresLot: true));
        with.LotId.Should().NotBeNull();
        (await TotalAsync(product)).Should().Be(16m);
    }

    [Theory]
    [InlineData(MovementType.ProductionReturn)]
    [InlineData(MovementType.ProductionScrap)]
    [InlineData(MovementType.ProductionIssue)]
    [InlineData(MovementType.SubcontractTransfer)]
    public async Task A_lot_controlled_item_needs_the_lot_named_for_return_scrap_issue_and_transfer(MovementType type)
    {
        var (product, lot) = await SeedAsync();

        var without = () => RunAsync(Open(), new(type, product, 1m, "MO-5#1", ToWarehouseId: _north, RequiresLot: true));
        (await without.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("WH_LOT_REQUIRED");
        (await TotalAsync(product)).Should().Be(14m);

        // a lot moves to another warehouse as a whole (10), the other types take 1
        var with = await RunAsync(Open(), new(type, product, type == MovementType.SubcontractTransfer ? 10m : 1m, "MO-5#2", LotId: lot, ToWarehouseId: _north, RequiresLot: true));
        with.Replayed.Should().BeFalse();
    }

    [Fact]
    public async Task An_unflagged_item_may_still_move_without_a_lot()
    {
        var (product, _) = await SeedAsync();

        var r = await RunAsync(Open(), new(MovementType.ProductionIssue, product, 1m, "MO-6#1"));

        r.Replayed.Should().BeFalse();
        (await TotalAsync(product)).Should().Be(13m);
    }

    [Fact]
    public async Task A_lot_controlled_issue_against_a_reservation_is_refused_when_it_would_draw_lot_less_stock_and_accepted_from_lots()
    {
        var (product, lot) = await SeedAsync();
        var c = Open();
        var reserve = new ReserveForSourceCommandHandler(c.Uow, c.Ledger, c.Cache);
        var reservation = await reserve.Handle(new ReserveForSourceCommand
        {
            SourceId = Guid.NewGuid(),
            WarehouseId = _main,
            Lines = [new SourceReservationLineDto { ProductId = product, Quantity = 12m }]   // 10 from the lot, 2 lot-less
        }, default);

        var tooMuch = () => RunAsync(Open(), new(MovementType.ProductionIssue, product, 11m, "MO-7#1", ReservationId: reservation.ReservationId, RequiresLot: true));
        (await tooMuch.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("WH_LOT_REQUIRED");
        (await TotalAsync(product)).Should().Be(14m);

        var fromLots = await RunAsync(Open(), new(MovementType.ProductionIssue, product, 10m, "MO-7#2", ReservationId: reservation.ReservationId, RequiresLot: true));
        fromLots.LotId.Should().Be(lot);
        (await TotalAsync(product)).Should().Be(4m);
    }

    // ── expired lots (TC-MAT-009) ───────────────────────────────────────────────────────────────────

    private async Task<(Guid Product, Guid Expired, Guid Fresh)> SeedWithExpiredLotAsync()
    {
        var c = Open();
        var item = new InventoryEntity(Guid.NewGuid(), "Paint", 0, 0);
        item.SetValuationMethod(ValuationMethod.Average);
        await c.Uow.Inventories.AddAsync(item, default);
        var expired = await c.Ledger.ReceiveAsync(item, 20m, 10m, new StockTarget(_main, null), new NewLot("L-X", 10m, DateTime.UtcNow.AddDays(-9), null), default);
        var fresh = await c.Ledger.ReceiveAsync(item, 30m, 10m, new StockTarget(_main, null), new NewLot("L-A", 10m, DateTime.UtcNow.AddDays(20), null), default);
        await c.Uow.SaveChangesAsync(default);
        return (item.ProductId, expired!.LotId, fresh!.LotId);
    }

    [Fact]
    public async Task Issuing_from_an_expired_lot_is_refused_and_a_fresh_lot_is_accepted()
    {
        var (product, expired, fresh) = await SeedWithExpiredLotAsync();

        var act = () => RunAsync(Open(), new(MovementType.ProductionIssue, product, 5m, "MO-9#1", LotId: expired));
        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("WH_LOT_EXPIRED");
        (await TotalAsync(product)).Should().Be(50m);

        (await RunAsync(Open(), new(MovementType.ProductionIssue, product, 5m, "MO-9#2", LotId: fresh))).Replayed.Should().BeFalse();
    }

    [Fact]
    public async Task An_expired_lot_can_still_be_scrapped_and_returned_to()
    {
        var (product, expired, _) = await SeedWithExpiredLotAsync();

        (await RunAsync(Open(), new(MovementType.ProductionScrap, product, 20m, "MO-9#S", LotId: expired))).Replayed.Should().BeFalse();
        (await RunAsync(Open(), new(MovementType.ProductionReturn, product, 1m, "MO-9#R", LotId: expired, UnitCost: 10m))).Replayed.Should().BeFalse();
        (await TotalAsync(product)).Should().Be(31m);
    }

    [Fact]
    public async Task A_reservation_whose_lot_expired_after_it_was_made_cannot_be_consumed_from_that_lot()
    {
        var (product, lot) = await SeedAsync();   // L-1: 10 in lot, 4 lot-less
        var c = Open();
        var reservation = await new ReserveForSourceCommandHandler(c.Uow, c.Ledger, c.Cache).Handle(new ReserveForSourceCommand
        {
            SourceId = Guid.NewGuid(),
            WarehouseId = _main,
            Lines = [new SourceReservationLineDto { ProductId = product, Quantity = 5m }]
        }, default);

        var e = Open();
        e.Db.Entry(await e.Db.InventoryLots.SingleAsync(l => l.LotId == lot)).Property(nameof(InventoryLot.ExpiryDate)).CurrentValue = DateTime.UtcNow.AddDays(-1);
        await e.Db.SaveChangesAsync();

        var act = () => RunAsync(Open(), new(MovementType.ProductionIssue, product, 2m, "MO-9#X", ReservationId: reservation.ReservationId));
        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("WH_LOT_EXPIRED");
        (await TotalAsync(product)).Should().Be(14m);
    }

    // ── lot status and release ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_lot_is_put_on_hold_then_blocked_and_the_same_status_changes_nothing()
    {
        var (_, lot) = await SeedAsync();
        var c = Open();
        var handler = new SetLotStatusCommandHandler(c.Uow, c.Ledger, c.Cache);

        (await handler.Handle(new SetLotStatusCommand { LotId = lot, Status = "QcHold" }, default)).Unchanged.Should().BeFalse();
        (await handler.Handle(new SetLotStatusCommand { LotId = lot, Status = "QcHold" }, default)).Unchanged.Should().BeTrue();
        (await handler.Handle(new SetLotStatusCommand { LotId = lot, Status = "blocked" }, default)).Status.Should().Be("Blocked");
        (await Open().Db.InventoryLots.SingleAsync(l => l.LotId == lot)).Status.Should().Be(LotStatus.Blocked);
    }

    [Fact]
    public async Task Available_is_not_a_target_of_the_status_call_and_an_unknown_lot_is_not_found()
    {
        var (_, lot) = await SeedAsync();
        var c = Open();
        var handler = new SetLotStatusCommandHandler(c.Uow, c.Ledger, c.Cache);

        var toAvailable = () => handler.Handle(new SetLotStatusCommand { LotId = lot, Status = "Available" }, default);
        (await toAvailable.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("WH_LOT_STATUS");

        var unknown = () => handler.Handle(new SetLotStatusCommand { LotId = Guid.NewGuid(), Status = "QcHold" }, default);
        await unknown.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task A_lot_on_hold_is_released_by_the_quality_release_movement_and_is_then_allocatable()
    {
        var (product, lot) = await SeedAsync();
        var c = Open();
        await new SetLotStatusCommandHandler(c.Uow, c.Ledger, c.Cache).Handle(new SetLotStatusCommand { LotId = lot, Status = "QcHold" }, default);

        var onHold = () => RunAsync(Open(), new(MovementType.ProductionIssue, product, 1m, "MO-8#1", LotId: lot));
        (await onHold.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("WH_LOT_NOT_USABLE");

        await RunAsync(Open(), new(MovementType.QcRelease, product, 0m, "MO-8#QC", LotId: lot));
        var after = await RunAsync(Open(), new(MovementType.ProductionIssue, product, 1m, "MO-8#2", LotId: lot));
        after.Replayed.Should().BeFalse();
    }

    [Fact]
    public async Task Releasing_a_reservation_twice_frees_the_stock_once_and_an_unknown_id_is_not_found()
    {
        var (product, _) = await SeedAsync();
        var c = Open();
        var reservation = await new ReserveForSourceCommandHandler(c.Uow, c.Ledger, c.Cache).Handle(new ReserveForSourceCommand
        {
            SourceId = Guid.NewGuid(),
            WarehouseId = _main,
            Lines = [new SourceReservationLineDto { ProductId = product, Quantity = 6m }]
        }, default);

        var release = Open();
        var handler = new ReleaseReservationCommandHandler(release.Uow, release.Cache, release.Ledger);
        var first = await handler.Handle(new ReleaseReservationCommand { ReservationId = reservation.ReservationId, Reason = "cancelled" }, default);
        var second = await new ReleaseReservationCommandHandler(Open().Uow, release.Cache, Open().Ledger)
            .Handle(new ReleaseReservationCommand { ReservationId = reservation.ReservationId, Reason = "again" }, default);

        first.Status.Should().Be("Released");
        second.Status.Should().Be("Released");
        (await Open().Db.Inventories.SingleAsync(i => i.ProductId == product)).ReservedQuantity.Should().Be(0m);

        var unknown = () => handler.Handle(new ReleaseReservationCommand { ReservationId = Guid.NewGuid(), Reason = "x" }, default);
        await unknown.Should().ThrowAsync<EntityNotFoundException>();
    }

    // ── lot list for Manufacturing (P3-5) ───────────────────────────────────────────────────────────

    [Fact]
    public async Task The_lot_list_shows_every_lot_of_the_item_also_the_used_up_ones()
    {
        var (product, lot) = await SeedAsync();
        await RunAsync(Open(), new(MovementType.ProductionIssue, product, 10m, "MO-1#1", LotId: lot));       // lot L-1 is now used up

        var c = Open();
        var lots = await new ERP.WH.Application.Queries.Inventory.ListProductLotsQueryHandler(c.Uow)
            .Handle(new ERP.WH.Application.Queries.Inventory.ListProductLotsQuery { ProductId = product }, default);

        var l1 = lots.Should().ContainSingle().Subject;
        (l1.LotNumber, l1.RemainingQuantity, l1.WarehouseId, l1.Status).Should().Be(("L-1", 0m, _main, "Available"));
        (await new ERP.WH.Application.Queries.Inventory.ListProductLotsQueryHandler(Open().Uow)
            .Handle(new ERP.WH.Application.Queries.Inventory.ListProductLotsQuery { ProductId = Guid.NewGuid() }, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task The_first_receipt_of_an_item_the_warehouse_never_held_creates_its_record_and_other_movements_still_need_one()
    {
        var product = Guid.NewGuid();

        var issue = () => RunAsync(Open(), new(MovementType.ProductionIssue, product, 1m, "MO-9#1"));
        await issue.Should().ThrowAsync<EntityNotFoundException>();

        var receipt = await RunAsync(Open(), new(MovementType.ProductionReceipt, product, 5m, "FR-9#1", WarehouseId: _main, LotNumber: "FG-1", ProductName: "Table", ProductSku: "TP-BAN01"));

        receipt.Replayed.Should().BeFalse();
        var inventory = await Open().Db.Inventories.SingleAsync(i => i.ProductId == product);
        (inventory.ProductName, inventory.ProductSku, inventory.TotalQuantity).Should().Be(("Table", "TP-BAN01", 5m));
        (await Open().Db.InventoryLots.SingleAsync(l => l.ProductId == product)).LotNumber.Should().Be("FG-1");
    }
}

using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.MFG.UnitTests.Orders;

/// <summary>A Warehouse in memory: balances per product, one open production reservation per order, and switches for an outage or a refusal.</summary>
internal sealed class FakeStock : IWarehouseStockClient
{
    public readonly Dictionary<Guid, List<StockBalanceInfo>> Balances = [];
    public readonly Dictionary<Guid, StockReservation> Reservations = [];
    public bool Down;
    public string? RefuseReserveWith;
    public int Reserves, Releases;

    public void Stock(Guid product, Guid warehouse, decimal quantity) => Add(product, new StockBalanceInfo(warehouse, "WH", null, null, null, null, quantity, 0, 0, quantity));

    public void Lot(Guid product, Guid warehouse, string number, decimal quantity, string status = "Available", DateTime? expiry = null)
        => Add(product, new StockBalanceInfo(warehouse, "WH", Guid.NewGuid(), number, status, expiry, quantity, 0, 0, quantity));

    private void Add(Guid product, StockBalanceInfo balance)
    {
        if (!Balances.TryGetValue(product, out var list)) Balances[product] = list = [];
        list.Add(balance);
    }

    private void Check()
    {
        if (Down) throw MfgRules.Fail(MfgErrors.Int001);
    }

    public Task<StockAvailability> GetAvailabilityAsync(Guid productId, Guid? warehouseId, CancellationToken ct = default)
    {
        Check();
        var rows = Balances.GetValueOrDefault(productId) ?? [];
        var held = Reservations.Values.Where(r => r.Status == "Reserved").SelectMany(r => r.Lines).Where(l => l.ProductId == productId).Sum(l => l.Remaining);
        var total = rows.Sum(b => b.Quantity);
        return Task.FromResult(new StockAvailability(productId, total, held, 0, Math.Max(total - held, 0), 0m, rows));
    }

    public Task<LotSuggestion> SuggestLotsAsync(Guid productId, decimal quantity, Guid? warehouseId, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<StockReservation> ReserveAsync(Guid productionOrderId, string? orderNumber, Guid? warehouseId, IReadOnlyList<StockReservationLine> lines, CancellationToken ct = default)
    {
        Check();
        if (RefuseReserveWith != null) throw new BusinessRuleException(RefuseReserveWith, "refused by the Warehouse");
        Reserves++;
        var reservation = new StockReservation(Guid.NewGuid(), "ProductionOrder", productionOrderId, orderNumber, "Reserved", false,
            lines.Select(l => new StockReservationLineResult(l.ProductId, l.SourceLineId, warehouseId, l.Quantity, 0, l.Quantity, [])).ToList());
        Reservations[productionOrderId] = reservation;
        return Task.FromResult(reservation);
    }

    public Task<StockReservation?> GetReservationAsync(Guid productionOrderId, CancellationToken ct = default)
    {
        Check();
        return Task.FromResult(Reservations.GetValueOrDefault(productionOrderId));
    }

    public Task<ReservationReleased> ReleaseReservationAsync(Guid reservationId, string? reason, CancellationToken ct = default)
    {
        Check();
        var pair = Reservations.FirstOrDefault(r => r.Value.ReservationId == reservationId);
        if (pair.Value != null && pair.Value.Status == "Reserved")
        {
            Releases++;
            Reservations[pair.Key] = pair.Value with { Status = "Released", Lines = [] };
        }
        return Task.FromResult(new ReservationReleased(reservationId, "Released", DateTime.UtcNow));
    }

    public Task<StockMovementResult> RecordMovementAsync(StockMovementRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<LotStatusResult> SetLotStatusAsync(Guid lotId, string status, CancellationToken ct = default) => throw new NotSupportedException();

    /// <summary>What the order holds now, by component id.</summary>
    public decimal HeldFor(Guid orderId, Guid product)
        => Reservations.GetValueOrDefault(orderId) is { Status: "Reserved" } r ? r.Lines.Where(l => l.ProductId == product).Sum(l => l.Remaining) : 0m;
}

internal sealed class SettingsFake : IProductionSettingProvider
{
    public bool BlockReleaseOnShortage;
    public bool Sequential = true;

    public Task<ProductionSettingValues> GetAsync(Guid plantId, CancellationToken ct = default)
        => Task.FromResult(ProductionSettingValues.Defaults with { BlockReleaseOnShortage = BlockReleaseOnShortage, RequireSequentialOperations = Sequential });

    public void Invalidate(Guid plantId) { }
}

/// <summary>DS-01 orders plus a fake Warehouse and the P3-2 handlers (test cases TC-PO-011..028).</summary>
internal sealed class ReleaseCtx : OrderCtx
{
    public FakeStock Stock { get; } = new();
    public SettingsFake PlantSettings { get; } = new();
    public ReasonCode HoldReason { get; }
    public ReasonCode CancelReason { get; }
    public Guid RawWarehouse => Plant.RawMaterialWarehouseId;

    public ReleaseCtx()
    {
        HoldReason = ReasonCode.Create(ReasonType.Hold, "HOLD-01", "Waiting for a drawing", null);
        CancelReason = ReasonCode.Create(ReasonType.Cancel, "CAN-01", "Customer cancelled", null);
        F.Reasons.Add(HoldReason);
        F.Reasons.Add(CancelReason);
    }

    public IOrderAvailability Availability => new OrderAvailabilityService(Stock, PlantTime);
    public OrderReservationSync Sync => new(Stock, Availability, F.Uow, PlantTime);

    /// <summary>Stock for the DS-01 components of 100 pieces of TP-BAN01 (book §1.7): everything in full, except BTP-CHAN01 (300 of the 400 needed).</summary>
    public void StockDs01(decimal chan = 300)
    {
        foreach (var (code, qty) in new[] { ("BTP-MAT01", 100m), ("NVL-VIT01", 1600m), ("NVL-SON01", 100m), ("NVL-THUNG01", 100m), ("NVL-XOP01", 200m), ("KIT-PKG01", 100m), ("NVL-KEO01", 100m), ("NVL-KEO02", 100m), ("NVL-GO01", 100m), ("PP-MUN01", 100m) })
            if (Items.TryGetValue(code, out var item)) Stock.Stock(item.Id, RawWarehouse, qty);
        Stock.Stock(Items["BTP-CHAN01"].Id, RawWarehouse, chan);
    }

    public Task<OrderActionResultDto> ReleaseAsync(Guid id)
        => new ReleaseOrderHandler(Access, Repo, F.ProductClient, F.Catalog, PlantSettings, Availability, Sync, User, F.Uow).Handle(new ReleaseOrderCommand(id), default);

    public Task<OrderActionResultDto> UnreleaseAsync(Guid id)
        => new UnreleaseOrderHandler(Access, Repo, new OrderTransactionGuard(), Sync, User, F.Uow).Handle(new UnreleaseOrderCommand(id), default);

    public Task<OrderActionResultDto> CancelAsync(Guid id, Guid? reason)
        => new CancelOrderHandler(Access, Repo, F.ReasonRepo, new OrderTransactionGuard(), Sync, User, F.Uow).Handle(new CancelOrderCommand(id, reason, "note"), default);

    public Task<OrderActionResultDto> HoldAsync(Guid id, Guid? reason)
        => new HoldOrderHandler(Access, Repo, F.ReasonRepo, User, F.Uow).Handle(new HoldOrderCommand(id, reason, null), default);

    public Task<OrderActionResultDto> ResumeAsync(Guid id)
        => new ResumeOrderHandler(Access, Repo, User, F.Uow).Handle(new ResumeOrderCommand(id), default);

    public Task<OrderActionResultDto> ChangeQuantityAsync(Guid id, decimal quantity)
        => new ChangeOrderQuantityHandler(Access, Repo, Builder(), Sync, User, F.Uow).Handle(new ChangeOrderQuantityCommand(id, quantity, "more"), default);

    public Task<OrderAvailabilityDto> AvailabilityAsync(Guid id)
        => new GetOrderAvailabilityHandler(Access, Repo, Availability).Handle(new GetOrderAvailabilityQuery(id), default);

    public async Task<OrderDto> DraftAsync(decimal quantity = 100)
    {
        await SeedAllAsync();
        StockDs01();
        return await CreateOrderAsync("TP-BAN01", quantity);
    }
}

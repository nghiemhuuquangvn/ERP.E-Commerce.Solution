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

    public Guid Lot(Guid product, Guid warehouse, string number, decimal quantity, string status = "Available", DateTime? expiry = null)
    {
        var id = Guid.NewGuid();
        Add(product, new StockBalanceInfo(warehouse, "WH", id, number, status, expiry, quantity, 0, 0, quantity));
        return id;
    }

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

    /// <summary>First-expiry-first over the usable lots (a lot without expiry comes last), like the Warehouse's <c>LotPicker</c>.</summary>
    public Task<LotSuggestion> SuggestLotsAsync(Guid productId, decimal quantity, Guid? warehouseId, CancellationToken ct = default)
    {
        Check();
        var rows = (Balances.GetValueOrDefault(productId) ?? [])
            .Where(b => b.LotId != null && b.LotStatus == "Available" && (b.ExpiryDate is null || b.ExpiryDate > DateTime.UtcNow) && b.AvailableQuantity > 0)
            .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue).ToList();
        var left = quantity;
        var lots = new List<SuggestedLotInfo>();
        foreach (var row in rows)
        {
            if (left <= 0) break;
            var take = Math.Min(row.AvailableQuantity, left);
            lots.Add(new SuggestedLotInfo(row.LotId!.Value, row.LotNumber ?? "", row.WarehouseId, row.ExpiryDate, row.AvailableQuantity, take));
            left -= take;
        }
        return Task.FromResult(new LotSuggestion(productId, warehouseId, quantity, lots, left, 0m));
    }

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

    // ── Movements (P3-3): keys deduplicate like the real Warehouse, issues against a reservation consume it, a hook injects outages and refusals ──────────
    public sealed record Booked(string Key, string Type, Guid ProductId, decimal Quantity, Guid? LotId, Guid? ReservationId, decimal UnitCost, Guid MovementId);

    public readonly List<Booked> Movements = [];
    public readonly List<StockMovementRequest> Requests = [];
    public readonly Dictionary<Guid, decimal> Costs = [];
    public Func<StockMovementRequest, int, Exception?>? Intercept;
    public int MovementCalls;

    public decimal CostOf(Guid product) => Costs.GetValueOrDefault(product, 2m);

    public Task<StockMovementResult> RecordMovementAsync(StockMovementRequest r, CancellationToken ct = default)
    {
        MovementCalls++;
        Requests.Add(r);
        if (Intercept?.Invoke(r, MovementCalls) is { } failure) throw failure;
        if (Down) throw MfgRules.Fail(MfgErrors.Int001);

        var existing = Movements.FirstOrDefault(m => m.Key == r.IdempotencyKey && m.ProductId == r.ProductId && m.Type == r.MovementType);
        if (existing != null)
        {
            if (existing.Quantity != r.Quantity) throw new ConcurrencyException("WH_IDEMPOTENCY_CONFLICT", "same key, other quantity");
            return Task.FromResult(new StockMovementResult(existing.MovementId, existing.Type, existing.Quantity, DateTime.UtcNow, existing.UnitCost, Math.Round(existing.Quantity * existing.UnitCost, 2), r.WarehouseId, existing.LotId, true));
        }

        decimal unitCost;
        if (r.MovementType == "ProductionIssue")
        {
            unitCost = CostOf(r.ProductId);
            if (r.ReservationId is { } reservationId)
            {
                var pair = Reservations.FirstOrDefault(x => x.Value.ReservationId == reservationId);
                if (pair.Value is null || pair.Value.Status != "Reserved") throw new BusinessRuleException("WH_RESERVATION_NOT_OPEN", "The reservation is not open.");
                var index = pair.Value.Lines.FindIndex(l => l.ProductId == r.ProductId && (r.SourceLineId == null || l.SourceLineId == r.SourceLineId));
                if (index < 0) throw new BusinessRuleException("WH_RESERVATION_LINE", "No such line.");
                var line = pair.Value.Lines[index];
                if (line.Remaining < r.Quantity) throw new BusinessRuleException("WH_RESERVATION_EXCEEDED", "More than reserved.");
                var lines = pair.Value.Lines.ToList();
                lines[index] = line with { Consumed = line.Consumed + r.Quantity, Remaining = line.Remaining - r.Quantity };
                Reservations[pair.Key] = pair.Value with { Lines = lines, Status = lines.All(l => l.Remaining == 0) ? "Fulfilled" : "Reserved" };
            }
            else
            {
                var held = Reservations.Values.Where(x => x.Status == "Reserved").SelectMany(x => x.Lines).Where(l => l.ProductId == r.ProductId).Sum(l => l.Remaining);
                var rows = Balances.GetValueOrDefault(r.ProductId) ?? [];
                if (r.LotId is { } lotId)
                {
                    var row = rows.FirstOrDefault(b => b.LotId == lotId) ?? throw new BusinessRuleException("WH_LOT_NOT_FOUND", "No such lot.");
                    if (row.LotStatus != "Available") throw new BusinessRuleException("WH_LOT_NOT_USABLE", "Lot on hold.");
                    if (row.ExpiryDate is { } expiry && expiry <= DateTime.UtcNow) throw new BusinessRuleException("WH_LOT_EXPIRED", "Lot expired.");
                    if (row.Quantity < r.Quantity) throw new BusinessRuleException("WH_INSUFFICIENT_STOCK", "Not enough in the lot.");
                }
                if (!r.AllowNegative && rows.Sum(b => b.Quantity) - held < r.Quantity) throw new BusinessRuleException("WH_INSUFFICIENT_STOCK", "Not enough free stock.");
            }
            Adjust(r.ProductId, r.LotId, -r.Quantity);
        }
        else if (r.MovementType == "ProductionReturn")
        {
            unitCost = r.UnitCost ?? CostOf(r.ProductId);
            Adjust(r.ProductId, r.LotId, r.Quantity);
        }
        else throw new BusinessRuleException("WH_MOVEMENT_TYPE", r.MovementType + " is not supported by the fake.");

        var booked = new Booked(r.IdempotencyKey, r.MovementType, r.ProductId, r.Quantity, r.LotId, r.ReservationId, unitCost, Guid.NewGuid());
        Movements.Add(booked);
        var sign = r.MovementType == "ProductionIssue" ? -1m : 1m;
        return Task.FromResult(new StockMovementResult(booked.MovementId, r.MovementType, r.Quantity, DateTime.UtcNow, unitCost, sign * Math.Round(r.Quantity * unitCost, 2), r.WarehouseId, r.LotId, false));
    }

    public Task<LotStatusResult> SetLotStatusAsync(Guid lotId, string status, CancellationToken ct = default) => throw new NotSupportedException();

    /// <summary>Changes the quantity of one balance row (the lot's, else the lot-less one; a missing row is created).</summary>
    public void Adjust(Guid product, Guid? lot, decimal delta)
    {
        if (!Balances.TryGetValue(product, out var rows)) Balances[product] = rows = [];
        var index = rows.FindIndex(b => b.LotId == lot);
        if (index < 0)
        {
            rows.Add(new StockBalanceInfo(rows.FirstOrDefault()?.WarehouseId, "WH", lot, null, lot is null ? null : "Available", null, delta, 0, 0, delta));
            return;
        }
        var row = rows[index];
        rows[index] = row with { Quantity = row.Quantity + delta, AvailableQuantity = row.AvailableQuantity + delta };
    }

    public decimal Total(Guid product) => (Balances.GetValueOrDefault(product) ?? []).Sum(b => b.Quantity);

    /// <summary>What the order holds now, by component id.</summary>
    public decimal HeldFor(Guid orderId, Guid product)
        => Reservations.GetValueOrDefault(orderId) is { Status: "Reserved" } r ? r.Lines.Where(l => l.ProductId == product).Sum(l => l.Remaining) : 0m;
}

internal sealed class SettingsFake : IProductionSettingProvider
{
    public bool BlockReleaseOnShortage;
    public bool Sequential = true;
    public decimal OverIssueTolerancePct = 3m;
    public decimal OverProductionTolerancePct = 5m;
    public bool AllowNegativeStockOnBackflush;

    public Task<ProductionSettingValues> GetAsync(Guid plantId, CancellationToken ct = default)
        => Task.FromResult(ProductionSettingValues.Defaults with
        {
            BlockReleaseOnShortage = BlockReleaseOnShortage, RequireSequentialOperations = Sequential, OverIssueTolerancePct = OverIssueTolerancePct,
            OverProductionTolerancePct = OverProductionTolerancePct, AllowNegativeStockOnBackflush = AllowNegativeStockOnBackflush,
        });

    public void Invalidate(Guid plantId) { }
}

/// <summary>DS-01 orders plus a fake Warehouse and the P3-2 handlers (test cases TC-PO-011..028).</summary>
internal class ReleaseCtx : OrderCtx
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

    public StockDocRepoFake Documents { get; } = new();
    public TimeLogRepoFake TimeLogs { get; } = new();
    public ReportRepoFake Reports { get; } = new();

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
        => new UnreleaseOrderHandler(Access, Repo, new OrderTransactionGuard(Documents), Sync, User, F.Uow).Handle(new UnreleaseOrderCommand(id), default);

    public Task<OrderActionResultDto> CancelAsync(Guid id, Guid? reason)
        => new CancelOrderHandler(Access, Repo, F.ReasonRepo, new OrderTransactionGuard(Documents), Sync, User, F.Uow).Handle(new CancelOrderCommand(id, reason, "note"), default);

    public Task<OrderActionResultDto> HoldAsync(Guid id, Guid? reason)
        => new HoldOrderHandler(Access, Repo, F.ReasonRepo, TimeLogs, PlantTime, User, F.Uow).Handle(new HoldOrderCommand(id, reason, null), default);

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

using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;
using InventoryEntity = ERP.WH.Domain.Aggregates.Inventory.Inventory;

namespace ERP.WH.Application.Services;

/// <summary>Where a stock change lands: a warehouse (null = unassigned) and optionally a bin.</summary>
public sealed record StockTarget(Guid? WarehouseId, Guid? LocationId);

/// <summary>A lot created by a receipt.</summary>
public sealed record NewLot(string Number, decimal UnitCost, DateTime? ExpiryDate, Guid? PurchaseOrderId, DateTime? ManufactureDate = null, LotSource Source = LotSource.Receipt);

public enum IssuePolicy
{
    /// <summary>Refuse when the item total is below the quantity (manual sale).</summary>
    Refuse,
    /// <summary>The goods already left (point of sale): stock may go below zero.</summary>
    AllowNegative
}

public enum LotDepletion
{
    /// <summary>Do not touch lots.</summary>
    None,
    Fifo,
    Lifo
}

public sealed record ReserveResult(string Location, IReadOnlyList<LotAllocation> Allocations);

/// <summary>What shipping took out of one lot.</summary>
public sealed record ConsumedLot(InventoryLot Lot, decimal Quantity);

/// <summary>
/// The one place that changes stock quantities: <c>Inventory</c> (the per-item total), <c>InventoryLot</c>, <c>InventoryLocationStock</c> and
/// <c>StockBalance</c> (per warehouse and lot) move together, so they cannot drift. It never saves: the handler keeps its document logic
/// (movement, value, events, idempotency) and its single <c>SaveChangesAsync</c>, which also commits the outbox rows.
/// Adding or updating the <c>Inventory</c> row itself stays with the handler.
/// </summary>
public interface IStockLedger
{
    /// <summary>Warehouse of a change: the one named, else the one of the bin, else the tenant's default (null = unassigned).</summary>
    Task<StockTarget> TargetAsync(Guid? warehouseId, Guid? locationId, CancellationToken ct);

    /// <summary>Stock in. A lot number makes a new lot that takes the whole quantity; without one the quantity is lot-less. Returns the new lot, if any.</summary>
    Task<InventoryLot?> ReceiveAsync(InventoryEntity inventory, decimal quantity, decimal unitCost, StockTarget target, NewLot? lot, CancellationToken ct);

    /// <summary>Stock out that is not a reserved shipment (manual sale, point-of-sale). Lots are depleted first when asked.</summary>
    Task IssueAsync(InventoryEntity inventory, decimal quantity, StockTarget target, IssuePolicy policy, LotDepletion depletion, CancellationToken ct);

    /// <summary>A signed correction (adjustment, count variance, vendor return, void). Lot-less.</summary>
    Task AdjustAsync(InventoryEntity inventory, decimal delta, StockTarget target, CancellationToken ct);

    /// <summary>Soft hold: the item total, the first bin that can cover it and the lots first-expiry-first (Available lots only).</summary>
    Task<ReserveResult> ReserveAsync(InventoryEntity inventory, decimal quantity, StockTarget target, CancellationToken ct);

    /// <summary>Undoes the soft holds of a reservation (cancel, expiry).</summary>
    Task ReleaseAsync(Reservation reservation, CancellationToken ct);

    /// <summary>Ships a reserved item: the stock leaves the item total, the bin, the lots and the balances. Returns what each lot gave.</summary>
    Task<IReadOnlyList<ConsumedLot>> ConsumeAsync(InventoryEntity inventory, ReservedItem item, CancellationToken ct);

    /// <summary>Takes part of a reserved line out (partial consume): the named lot first, else the recorded lot split in order, then the warehouse pool.</summary>
    Task<IReadOnlyList<ConsumedLot>> ConsumeAsync(InventoryEntity inventory, ReservedItem item, decimal quantity, Guid? lotId, CancellationToken ct);

    /// <summary>Holds stock for a source document (production order) in ONE warehouse: its usable lots first-expiry-first, then its lot-less stock. A pinned lot is the only one used.</summary>
    Task<ReserveResult> ReserveForSourceAsync(InventoryEntity inventory, decimal quantity, Guid? warehouseId, Guid? pinnedLotId, CancellationToken ct);

    /// <summary>What a warehouse can still give to a new reservation: usable lot quantity not held plus lot-less quantity not held.</summary>
    Task<decimal> AvailableInWarehouseAsync(Guid productId, Guid? warehouseId, CancellationToken ct);

    /// <summary>A bin-to-bin move; the balances follow when the bins are in different warehouses.</summary>
    Task TransferAsync(Guid productId, Guid fromLocationId, Guid toLocationId, decimal quantity, CancellationToken ct);

    /// <summary>Production issue / scrap out of one named lot (no reservation involved): the item total, the lot and its balance go down.</summary>
    Task IssueFromLotAsync(InventoryEntity inventory, InventoryLot lot, decimal quantity, IssuePolicy policy, CancellationToken ct);

    /// <summary>Stock back into the lot it came from (production return): the item total is re-averaged at the cost passed.</summary>
    Task ReturnToLotAsync(InventoryEntity inventory, InventoryLot lot, decimal quantity, decimal unitCost, CancellationToken ct);

    /// <summary>Changes the status of a lot (hold, release, block). The only place a lot status is written.</summary>
    Task SetLotStatusAsync(InventoryLot lot, LotStatus status, CancellationToken ct);

    /// <summary>
    /// Moves lot-less stock, or (part of) a lot, from one warehouse to another; the item total does not change. The whole lot moves as it is; a part of
    /// it (W7) is split off into a new sub-lot that sits in the target warehouse - only the part that is not reserved can leave. Returns the lot move
    /// (null for lot-less stock or when the warehouses are the same).
    /// </summary>
    Task<LotMove?> MoveBetweenWarehousesAsync(Guid productId, Guid? fromWarehouseId, Guid? toWarehouseId, decimal quantity, InventoryLot? lot, CancellationToken ct);
}

/// <summary>
/// What a lot move did: <c>Moved</c> is the lot that now sits in the target warehouse (the same lot for a whole move, the NEW sub-lot for a split);
/// <c>SplitFrom</c> is the lot it was split off from (null for a whole move).
/// </summary>
public sealed record LotMove(InventoryLot Moved, InventoryLot? SplitFrom);

public sealed class StockLedger(IWarehouseUnitOfWork uow, IWarehouseResolver warehouses) : IStockLedger
{
    public async Task<StockTarget> TargetAsync(Guid? warehouseId, Guid? locationId, CancellationToken ct)
    {
        if (warehouseId.HasValue) return new StockTarget(warehouseId, locationId);
        if (locationId.HasValue)
        {
            var ofLocation = await warehouses.ForLocationAsync(locationId.Value, ct);
            if (ofLocation.HasValue) return new StockTarget(ofLocation, locationId);
        }
        return new StockTarget(await warehouses.DefaultAsync(ct), locationId);
    }

    // ── receive ──────────────────────────────────────────────────────────────────────────────────────────────
    public async Task<InventoryLot?> ReceiveAsync(InventoryEntity inventory, decimal quantity, decimal unitCost, StockTarget target, NewLot? lot, CancellationToken ct)
    {
        inventory.ReceiveStock(quantity, unitCost);

        if (target.LocationId.HasValue)
        {
            var locationStock = await GetOrAddLocationStockAsync(inventory.ProductId, target.LocationId.Value, ct);
            locationStock.Receive(quantity);
        }

        if (lot == null)
        {
            await AddToPoolAsync(inventory.ProductId, target.WarehouseId, quantity, ct);
            return null;
        }

        var created = new InventoryLot(inventory.ProductId, lot.Number, quantity, lot.UnitCost, lot.ExpiryDate, lot.PurchaseOrderId, target.WarehouseId, lot.ManufactureDate, lot.Source);
        await uow.InventoryLots.AddAsync(created, ct);
        var balance = await BalanceAsync(inventory.ProductId, target.WarehouseId, created.LotId, ct);
        balance.Add(quantity);
        return created;
    }

    // ── issue / adjust ───────────────────────────────────────────────────────────────────────────────────────
    public async Task IssueAsync(InventoryEntity inventory, decimal quantity, StockTarget target, IssuePolicy policy, LotDepletion depletion, CancellationToken ct)
    {
        if (policy == IssuePolicy.Refuse) inventory.SellStock(quantity);
        else inventory.RecordPointOfSaleSale(quantity);

        var remaining = quantity;
        if (depletion != LotDepletion.None)
        {
            var lots = (await uow.InventoryLots.GetByProductIdAsync(inventory.ProductId, includeDepleted: false, ct)).Where(l => l.IsUsable()).ToList();
            var ordered = depletion == LotDepletion.Fifo ? lots.OrderBy(l => l.ReceivedDate) : lots.OrderByDescending(l => l.ReceivedDate);
            foreach (var lot in ordered)
            {
                if (remaining <= 0) break;
                var take = Math.Min(lot.RemainingQuantity, remaining);
                if (take <= 0) continue;
                lot.Deplete(take);
                await uow.InventoryLots.UpdateAsync(lot, ct);
                (await BalanceAsync(inventory.ProductId, lot.WarehouseId, lot.LotId, ct)).Add(-take);
                remaining -= take;
            }
        }

        // a sale of free stock leaves every reservation alone (since W4 SellStock no longer lowers the item reservation either)
        if (remaining > 0) await TakeFromPoolAsync(inventory.ProductId, target.WarehouseId, remaining, ct);
    }

    public async Task AdjustAsync(InventoryEntity inventory, decimal delta, StockTarget target, CancellationToken ct)
    {
        inventory.AdjustStock(delta);

        if (target.LocationId.HasValue)
        {
            var locationStock = await GetOrAddLocationStockAsync(inventory.ProductId, target.LocationId.Value, ct);
            locationStock.AdjustStock(delta);
        }

        if (delta >= 0) await AddToPoolAsync(inventory.ProductId, target.WarehouseId, delta, ct);
        else await TakeFromPoolAsync(inventory.ProductId, target.WarehouseId, -delta, ct);
    }

    // ── reserve / release / consume ──────────────────────────────────────────────────────────────────────────
    public async Task<ReserveResult> ReserveAsync(InventoryEntity inventory, decimal quantity, StockTarget target, CancellationToken ct)
    {
        inventory.ReserveStock(quantity);

        var location = "DEFAULT_LOCATION";
        var locationStocks = await uow.InventoryLocationStocks.GetByProductIdAsync(inventory.ProductId, ct);
        var candidate = locationStocks.FirstOrDefault(s => s.CanReserve(quantity));
        if (candidate != null)
        {
            candidate.ReserveStock(quantity);
            await uow.InventoryLocationStocks.UpdateAsync(candidate, ct);
            location = candidate.LocationId.ToString();
        }

        // first-expiry-first over the lots that may be allocated; the soft hold leaves RemainingQuantity alone
        var lots = await uow.InventoryLots.GetByProductIdAsync(inventory.ProductId, includeDepleted: false, ct);
        var allocations = new List<LotAllocation>();
        var (picks, _) = LotPicker.Pick(lots, quantity, null, restrictToWarehouse: false, DateTime.UtcNow);
        foreach (var (lot, take) in picks)
        {
            lot.Reserve(take);
            await uow.InventoryLots.UpdateAsync(lot, ct);
            (await BalanceAsync(inventory.ProductId, lot.WarehouseId, lot.LotId, ct)).AddReserved(take);
            allocations.Add(new LotAllocation { LotId = lot.LotId, Quantity = take });
        }

        // what no lot covers is held on the lot-less balance of the warehouse
        var unattributed = quantity - allocations.Sum(a => a.Quantity);
        if (unattributed > 0)
            (await BalanceAsync(inventory.ProductId, target.WarehouseId, null, ct)).AddReserved(unattributed);

        return new ReserveResult(location, allocations);
    }

    public async Task ReleaseAsync(Reservation reservation, CancellationToken ct)
    {
        foreach (var item in reservation.ReservedItems)
        {
            var inventory = await uow.Inventories.GetByProductIdAsync(item.ProductId, ct);
            var held = item.RemainingQuantity;
            if (inventory != null)
            {
                inventory.ReleaseReservation(held);
                await uow.Inventories.UpdateAsync(inventory, ct);
            }

            if (Guid.TryParse(item.WarehouseLocation, out var locationId))
            {
                var locationStock = await uow.InventoryLocationStocks.GetAsync(item.ProductId, locationId, ct);
                if (locationStock != null)
                {
                    locationStock.ReleaseReservation(held);
                    await uow.InventoryLocationStocks.UpdateAsync(locationStock, ct);
                }
            }

            foreach (var allocation in item.LotAllocations)
            {
                if (allocation.Remaining <= 0) continue;
                var lot = await uow.InventoryLots.GetByIdAsync(allocation.LotId, ct);
                if (lot == null) continue;
                lot.ReleaseReservation(allocation.Remaining);
                await uow.InventoryLots.UpdateAsync(lot, ct);
                (await BalanceAsync(item.ProductId, lot.WarehouseId, lot.LotId, ct)).AddReserved(-allocation.Remaining);
            }

            var unattributed = held - item.LotAllocations.Sum(a => a.Remaining);
            if (unattributed > 0) await ReleaseFromPoolsAsync(item.ProductId, item.WarehouseId, unattributed, ct);
        }
    }

    public Task<IReadOnlyList<ConsumedLot>> ConsumeAsync(InventoryEntity inventory, ReservedItem item, CancellationToken ct)
        => ConsumeAsync(inventory, item, item.RemainingQuantity, null, ct);

    public async Task<IReadOnlyList<ConsumedLot>> ConsumeAsync(InventoryEntity inventory, ReservedItem item, decimal quantity, Guid? lotId, CancellationToken ct)
    {
        if (quantity <= 0 || quantity > item.RemainingQuantity)
            throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("WH_RESERVATION_EXCEEDED",
                $"Only {item.RemainingQuantity} of the reserved quantity is left to take; {quantity} was asked.");

        inventory.ConsumeReserved(quantity);

        if (Guid.TryParse(item.WarehouseLocation, out var locationId))
        {
            var locationStock = await uow.InventoryLocationStocks.GetAsync(item.ProductId, locationId, ct);
            if (locationStock != null)
            {
                locationStock.SellStock(quantity);
                await uow.InventoryLocationStocks.UpdateAsync(locationStock, ct);
            }
        }

        // the allocations are changed in place; the value comparer of the mapping sees the difference
        var remaining = quantity;
        var consumed = new List<ConsumedLot>();
        var allocations = lotId.HasValue
            ? item.LotAllocations.Where(a => a.LotId == lotId.Value).ToList()
            : item.LotAllocations.ToList();
        foreach (var allocation in allocations)
        {
            if (remaining <= 0) break;
            var take = Math.Min(allocation.Remaining, remaining);
            if (take <= 0) continue;
            var lot = await uow.InventoryLots.GetByIdAsync(allocation.LotId, ct);
            if (lot == null) continue;
            lot.Consume(take);
            await uow.InventoryLots.UpdateAsync(lot, ct);
            var balance = await BalanceAsync(item.ProductId, lot.WarehouseId, lot.LotId, ct);
            balance.Add(-take);
            balance.AddReserved(-take);
            allocation.Consumed += take;
            consumed.Add(new ConsumedLot(lot, take));
            remaining -= take;
        }

        if (lotId.HasValue && remaining > 0)
            throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("WH_RESERVATION_EXCEEDED",
                "The named lot holds less of this reservation than asked.");

        // what no lot covers comes off the lot-less stock of the line's warehouse
        if (remaining > 0)
        {
            var preferred = item.WarehouseId ?? (await TargetAsync(null, Guid.TryParse(item.WarehouseLocation, out var l2) ? l2 : null, ct)).WarehouseId;
            await TakeFromPoolAsync(item.ProductId, preferred, remaining, ct);
            await ReleaseFromPoolsAsync(item.ProductId, preferred, remaining, ct);
        }

        item.ConsumedQuantity += quantity;
        return consumed;
    }

    // ── reservation for a source document (W4) ───────────────────────────────────────────────────────────────
    public async Task<decimal> AvailableInWarehouseAsync(Guid productId, Guid? warehouseId, CancellationToken ct)
    {
        var lots = await uow.InventoryLots.GetByProductIdAsync(productId, includeDepleted: false, ct);
        var fromLots = LotPicker.Candidates(lots, warehouseId, restrictToWarehouse: true, DateTime.UtcNow).Sum(l => l.AvailableQuantity);
        var pool = await uow.StockBalances.GetAsync(productId, warehouseId, null, ct);
        return fromLots + (pool == null ? 0 : Math.Max(pool.Quantity - pool.ReservedQuantity - pool.LockedQuantity, 0));
    }

    public async Task<ReserveResult> ReserveForSourceAsync(InventoryEntity inventory, decimal quantity, Guid? warehouseId, Guid? pinnedLotId, CancellationToken ct)
    {
        inventory.ReserveStock(quantity);

        var lots = await uow.InventoryLots.GetByProductIdAsync(inventory.ProductId, includeDepleted: false, ct);
        var allocations = new List<LotAllocation>();
        var now = DateTime.UtcNow;

        if (pinnedLotId.HasValue)
        {
            var pinned = lots.Where(l => l.LotId == pinnedLotId.Value);
            var (picks, shortfall) = LotPicker.Pick(pinned, quantity, warehouseId, restrictToWarehouse: true, now);
            if (shortfall > 0)
                throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("WH_LOT_NOT_AVAILABLE",
                    "The named lot is not available in this warehouse for the quantity asked (on hold, expired, in another warehouse or already held).");
            foreach (var (lot, take) in picks) await HoldLotAsync(inventory.ProductId, lot, take, allocations, ct);
        }
        else
        {
            var (picks, shortfall) = LotPicker.Pick(lots, quantity, warehouseId, restrictToWarehouse: true, now);
            foreach (var (lot, take) in picks) await HoldLotAsync(inventory.ProductId, lot, take, allocations, ct);
            if (shortfall > 0)
            {
                var pool = await BalanceAsync(inventory.ProductId, warehouseId, null, ct);
                var free = pool.Quantity - pool.ReservedQuantity - pool.LockedQuantity;
                if (shortfall > free)
                    throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("WH_INSUFFICIENT_IN_WAREHOUSE",
                        $"The warehouse cannot cover {quantity}: {Math.Max(quantity - shortfall + free, 0)} is available there.");
                pool.AddReserved(shortfall);
            }
        }

        return new ReserveResult("DEFAULT_LOCATION", allocations);
    }

    private async Task HoldLotAsync(Guid productId, InventoryLot lot, decimal take, List<LotAllocation> allocations, CancellationToken ct)
    {
        lot.Reserve(take);
        await uow.InventoryLots.UpdateAsync(lot, ct);
        (await BalanceAsync(productId, lot.WarehouseId, lot.LotId, ct)).AddReserved(take);
        allocations.Add(new LotAllocation { LotId = lot.LotId, Quantity = take });
    }

    // ── transfer ─────────────────────────────────────────────────────────────────────────────────────────────
    public async Task TransferAsync(Guid productId, Guid fromLocationId, Guid toLocationId, decimal quantity, CancellationToken ct)
    {
        var source = await uow.InventoryLocationStocks.GetAsync(productId, fromLocationId, ct);
        if (source != null)
        {
            source.TransferOut(quantity);
            await uow.InventoryLocationStocks.UpdateAsync(source, ct);
        }

        var destination = await GetOrAddLocationStockAsync(productId, toLocationId, ct);
        destination.TransferIn(quantity);

        var fromWarehouse = (await TargetAsync(null, fromLocationId, ct)).WarehouseId;
        var toWarehouse = (await TargetAsync(null, toLocationId, ct)).WarehouseId;
        if (fromWarehouse != toWarehouse)
        {
            await TakeFromPoolAsync(productId, fromWarehouse, quantity, ct);
            await AddToPoolAsync(productId, toWarehouse, quantity, ct);
        }
    }

    // ── production movements (W3) ────────────────────────────────────────────────────────────────────────────
    public async Task IssueFromLotAsync(InventoryEntity inventory, InventoryLot lot, decimal quantity, IssuePolicy policy, CancellationToken ct)
    {
        if (policy == IssuePolicy.Refuse) inventory.SellStock(quantity);
        else inventory.RecordPointOfSaleSale(quantity);

        lot.Deplete(quantity);
        await uow.InventoryLots.UpdateAsync(lot, ct);
        (await BalanceAsync(inventory.ProductId, lot.WarehouseId, lot.LotId, ct)).Add(-quantity);
    }

    public async Task ReturnToLotAsync(InventoryEntity inventory, InventoryLot lot, decimal quantity, decimal unitCost, CancellationToken ct)
    {
        inventory.ReceiveStock(quantity, unitCost);
        lot.Replenish(quantity);
        await uow.InventoryLots.UpdateAsync(lot, ct);
        (await BalanceAsync(inventory.ProductId, lot.WarehouseId, lot.LotId, ct)).Add(quantity);
    }

    public Task SetLotStatusAsync(InventoryLot lot, LotStatus status, CancellationToken ct)
    {
        // The lot is already tracked (loaded, or added a moment ago by a receipt). Update() would turn an Added lot into Modified and the
        // tenant guard refuses a Modified row that has no tenant yet.
        lot.SetStatus(status);
        return Task.CompletedTask;
    }

    public const int MaxLotNumberLength = 100;

    public async Task<LotMove?> MoveBetweenWarehousesAsync(Guid productId, Guid? fromWarehouseId, Guid? toWarehouseId, decimal quantity, InventoryLot? lot, CancellationToken ct)
    {
        if (fromWarehouseId == toWarehouseId) return null;

        if (lot == null)
        {
            await TakeFromPoolAsync(productId, fromWarehouseId, quantity, ct);
            await AddToPoolAsync(productId, toWarehouseId, quantity, ct);
            return null;
        }

        if (quantity > lot.RemainingQuantity)
            throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("WH_LOT_QUANTITY",
                $"Lot {lot.LotNumber} holds {lot.RemainingQuantity}: {quantity} cannot be moved.");

        // a lot sits in exactly one warehouse: a part of it leaves as a NEW lot (sub-lot) that sits in the target warehouse
        if (quantity < lot.RemainingQuantity)
        {
            if (quantity > lot.AvailableQuantity)
                throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("WH_LOT_RESERVED",
                    $"Lot {lot.LotNumber} has {lot.ReservedQuantity} held by reservations: only {lot.AvailableQuantity} can be moved ({quantity} was asked).");

            var number = NextSubLotNumber(await uow.InventoryLots.GetByProductIdAsync(productId, includeDepleted: true, ct), lot);
            if (number.Length > MaxLotNumberLength)
                throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("WH_LOT_NUMBER_LENGTH",
                    $"The number of the new sub-lot ({number}) is longer than {MaxLotNumberLength} characters.");

            var source = await BalanceAsync(productId, lot.WarehouseId, lot.LotId, ct);
            var sub = lot.SplitOff(quantity, toWarehouseId, number);
            await uow.InventoryLots.UpdateAsync(lot, ct);
            await uow.InventoryLots.AddAsync(sub, ct);
            source.Add(-quantity);
            (await BalanceAsync(productId, toWarehouseId, sub.LotId, ct)).Add(quantity);
            return new LotMove(sub, lot);
        }

        // the whole lot moves as it is, with the quantity reserved on it
        var from = await BalanceAsync(productId, lot.WarehouseId, lot.LotId, ct);
        var reserved = from.ReservedQuantity;
        from.Add(-quantity);
        from.AddReserved(-reserved);
        lot.AssignWarehouse(toWarehouseId);
        await uow.InventoryLots.UpdateAsync(lot, ct);
        var to = await BalanceAsync(productId, toWarehouseId, lot.LotId, ct);
        to.Add(quantity);
        to.AddReserved(reserved);
        return new LotMove(lot, null);
    }

    /// <summary>
    /// The number of the next sub-lot of <paramref name="source"/>: "&lt;root&gt;-S&lt;n&gt;", the root being the topmost ancestor (a sub-lot of a sub-lot is
    /// named from the same root) and n the smallest positive integer not used by ANY lot of the product, used-up ones included, so a number is never reused.
    /// </summary>
    public static string NextSubLotNumber(IEnumerable<InventoryLot> productLots, InventoryLot source)
    {
        var all = productLots.ToList();
        var byId = all.GroupBy(l => l.LotId).ToDictionary(g => g.Key, g => g.First());
        var root = source;
        for (var depth = 0; root.ParentLotId is { } parentId && byId.TryGetValue(parentId, out var parent) && depth < 100; depth++) root = parent;

        var used = new HashSet<string>(all.Select(l => l.LotNumber), StringComparer.OrdinalIgnoreCase);
        var n = 1;
        while (used.Contains($"{root.LotNumber}-S{n}")) n++;
        return $"{root.LotNumber}-S{n}";
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────
    private async Task<InventoryLocationStock> GetOrAddLocationStockAsync(Guid productId, Guid locationId, CancellationToken ct)
    {
        var stock = await uow.InventoryLocationStocks.GetAsync(productId, locationId, ct);
        if (stock != null) return stock;
        stock = new InventoryLocationStock(productId, locationId);
        await uow.InventoryLocationStocks.AddAsync(stock, ct);
        return stock;
    }

    /// <summary>The balance of one key; created (and tracked as added) when it does not exist yet.</summary>
    private async Task<StockBalance> BalanceAsync(Guid productId, Guid? warehouseId, Guid? lotId, CancellationToken ct)
    {
        var balance = await uow.StockBalances.GetAsync(productId, warehouseId, lotId, ct);
        if (balance != null) return balance;
        balance = new StockBalance(productId, warehouseId, lotId);
        await uow.StockBalances.AddAsync(balance, ct);
        return balance;
    }

    private async Task AddToPoolAsync(Guid productId, Guid? warehouseId, decimal quantity, CancellationToken ct)
        => (await BalanceAsync(productId, warehouseId, null, ct)).Add(quantity);

    /// <summary>
    /// Takes a quantity off the lot-less stock: the named warehouse first, then the other warehouses (largest first); only what is still
    /// missing makes the named warehouse negative (stock that was never received there).
    /// </summary>
    private async Task TakeFromPoolAsync(Guid productId, Guid? preferred, decimal quantity, CancellationToken ct)
    {
        var need = quantity;
        var pools = (await uow.StockBalances.ListByProductAsync(productId, ct)).Where(b => b.LotId == null).ToList();
        foreach (var pool in pools.OrderBy(p => p.WarehouseId == preferred ? 0 : 1).ThenByDescending(p => p.Quantity))
        {
            if (need <= 0) break;
            var take = Math.Min(need, Math.Max(pool.Quantity, 0));
            if (take <= 0) continue;
            pool.Add(-take);
            need -= take;
        }
        if (need > 0) (await BalanceAsync(productId, preferred, null, ct)).Add(-need);
    }

    /// <summary>Lowers the lot-less reservations (the named warehouse first); a reservation that is not there is ignored.</summary>
    private async Task ReleaseFromPoolsAsync(Guid productId, Guid? preferred, decimal quantity, CancellationToken ct)
    {
        var need = quantity;
        var pools = (await uow.StockBalances.ListByProductAsync(productId, ct)).Where(b => b.LotId == null && b.ReservedQuantity > 0).ToList();
        foreach (var pool in pools.OrderBy(p => p.WarehouseId == preferred ? 0 : 1).ThenByDescending(p => p.ReservedQuantity))
        {
            if (need <= 0) break;
            var take = Math.Min(need, pool.ReservedQuantity);
            pool.AddReserved(-take);
            need -= take;
        }
    }
}

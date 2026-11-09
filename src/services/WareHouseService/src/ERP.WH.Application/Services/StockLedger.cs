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

    /// <summary>A bin-to-bin move; the balances follow when the bins are in different warehouses.</summary>
    Task TransferAsync(Guid productId, Guid fromLocationId, Guid toLocationId, decimal quantity, CancellationToken ct);
}

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

        if (remaining > 0) await TakeFromPoolAsync(inventory.ProductId, target.WarehouseId, remaining, ct);
        if (policy == IssuePolicy.Refuse) await ReleaseFromPoolsAsync(inventory.ProductId, target.WarehouseId, quantity, ct);
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
        var remaining = quantity;
        foreach (var lot in lots.Where(l => l.IsUsable()).OrderBy(l => l.ExpiryDate ?? DateTime.MaxValue).ThenBy(l => l.ReceivedDate))
        {
            if (remaining <= 0) break;
            var take = Math.Min(lot.AvailableQuantity, remaining);
            if (take <= 0) continue;
            lot.Reserve(take);
            await uow.InventoryLots.UpdateAsync(lot, ct);
            (await BalanceAsync(inventory.ProductId, lot.WarehouseId, lot.LotId, ct)).AddReserved(take);
            allocations.Add(new LotAllocation { LotId = lot.LotId, Quantity = take });
            remaining -= take;
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
            if (inventory != null)
            {
                inventory.ReleaseReservation(item.Quantity);
                await uow.Inventories.UpdateAsync(inventory, ct);
            }

            if (Guid.TryParse(item.WarehouseLocation, out var locationId))
            {
                var locationStock = await uow.InventoryLocationStocks.GetAsync(item.ProductId, locationId, ct);
                if (locationStock != null)
                {
                    locationStock.ReleaseReservation(item.Quantity);
                    await uow.InventoryLocationStocks.UpdateAsync(locationStock, ct);
                }
            }

            foreach (var allocation in item.LotAllocations)
            {
                var lot = await uow.InventoryLots.GetByIdAsync(allocation.LotId, ct);
                if (lot == null) continue;
                lot.ReleaseReservation(allocation.Quantity);
                await uow.InventoryLots.UpdateAsync(lot, ct);
                (await BalanceAsync(item.ProductId, lot.WarehouseId, lot.LotId, ct)).AddReserved(-allocation.Quantity);
            }

            var unattributed = item.Quantity - item.LotAllocations.Sum(a => a.Quantity);
            if (unattributed > 0) await ReleaseFromPoolsAsync(item.ProductId, null, unattributed, ct);
        }
    }

    public async Task<IReadOnlyList<ConsumedLot>> ConsumeAsync(InventoryEntity inventory, ReservedItem item, CancellationToken ct)
    {
        inventory.SellStock(item.Quantity);

        if (Guid.TryParse(item.WarehouseLocation, out var locationId))
        {
            var locationStock = await uow.InventoryLocationStocks.GetAsync(item.ProductId, locationId, ct);
            if (locationStock != null)
            {
                locationStock.SellStock(item.Quantity);
                await uow.InventoryLocationStocks.UpdateAsync(locationStock, ct);
            }
        }

        var consumed = new List<ConsumedLot>();
        foreach (var allocation in item.LotAllocations)
        {
            var lot = await uow.InventoryLots.GetByIdAsync(allocation.LotId, ct);
            if (lot == null) continue;
            lot.Consume(allocation.Quantity);
            await uow.InventoryLots.UpdateAsync(lot, ct);
            var balance = await BalanceAsync(item.ProductId, lot.WarehouseId, lot.LotId, ct);
            balance.Add(-allocation.Quantity);
            balance.AddReserved(-allocation.Quantity);
            consumed.Add(new ConsumedLot(lot, allocation.Quantity));
        }

        var unattributed = item.Quantity - item.LotAllocations.Sum(a => a.Quantity);
        if (unattributed > 0)
        {
            var preferred = (await TargetAsync(null, Guid.TryParse(item.WarehouseLocation, out var l2) ? l2 : null, ct)).WarehouseId;
            await TakeFromPoolAsync(item.ProductId, preferred, unattributed, ct);
            await ReleaseFromPoolsAsync(item.ProductId, preferred, unattributed, ct);
        }

        return consumed;
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

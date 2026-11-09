using ERP.Shared.Infrastructure.Exceptions;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;
using InventoryEntity = ERP.WH.Domain.Aggregates.Inventory.Inventory;

namespace ERP.WH.Application.Services;

/// <summary>One production movement (docs/business/Warehouse/ProductionMovements.md). <c>Reference</c> is "&lt;document number&gt;#&lt;line&gt;" and is the idempotency key.</summary>
public sealed record ProductionMovementRequest(
    MovementType Type,
    Guid ProductId,
    decimal Quantity,
    string Reference,
    Guid? WarehouseId = null,
    Guid? ToWarehouseId = null,
    Guid? LotId = null,
    string? LotNumber = null,
    DateTime? ExpiryDate = null,
    DateTime? ManufactureDate = null,
    decimal? UnitCost = null,
    LotStatus? InitialLotStatus = null,
    bool AllowNegative = false,
    string? Reason = null,
    Guid? SourceOrderId = null,
    Guid? ReservationId = null,
    Guid? SourceLineId = null,
    bool RequiresLot = false,
    bool RequiresSerial = false);

/// <summary>What happened. <c>Replayed</c> = the same reference had been booked before, so nothing was changed.</summary>
public sealed record ProductionMovementResult(
    StockMovement Movement,
    bool Replayed,
    decimal PreviousQuantity,
    decimal CurrentQuantity,
    decimal UnitCost,
    decimal ValueDelta,
    decimal StockDelta,
    Guid? WarehouseId,
    Guid? LotId);

public interface IProductionMovementService
{
    Task<ProductionMovementResult> RecordAsync(ProductionMovementRequest request, CancellationToken ct);
}

/// <summary>
/// The production movements on top of <see cref="IStockLedger"/>: issue, return, receipt, scrap, quality release, subcontract transfer.
/// It never saves (the caller stages the events and commits once). Value rules: issue / scrap at the current cost (FIFO / LIFO: the lot's cost),
/// return and receipt at the cost passed, release and transfer carry no value. Accounting books none of them until the WIP posting of phase 6.
/// </summary>
public sealed class ProductionMovementService(IWarehouseUnitOfWork uow, IStockLedger ledger) : IProductionMovementService
{
    public async Task<ProductionMovementResult> RecordAsync(ProductionMovementRequest r, CancellationToken ct)
    {
        if (!r.Type.IsProduction())
            throw new BusinessRuleException("WH_MOVEMENT_TYPE", $"{r.Type} is not a production movement.");
        if (string.IsNullOrWhiteSpace(r.Reference))
            throw new BusinessRuleException("WH_REFERENCE_REQUIRED", "A production movement needs a reference: the document number and line, e.g. MO-2611-00001#1.");
        if (r.Type != MovementType.QcRelease && r.Quantity <= 0)
            throw new BusinessRuleException("WH_QUANTITY", "The quantity must be greater than zero.");

        // Serial-controlled items: no per-serial tracking on the production path yet (docs/business/Warehouse/ProductionIntegration.md) - refuse before anything is read or changed.
        if (r.RequiresSerial && r.Type != MovementType.QcRelease)
            throw new BusinessRuleException("WH_SERIAL_NOT_SUPPORTED", "Serial-controlled items cannot be moved by production movements yet.");

        // Replay: the same document line was booked before - answer with the first result and touch nothing.
        var reference = r.Reference.Trim();
        var existing = await uow.StockMovements.FindByKeyAsync(r.ProductId, reference, r.Type, ct);
        if (existing != null)
        {
            // The key names one document line: the same key with a different payload is a caller error, never a silent replay.
            if ((r.Type != MovementType.QcRelease && existing.Quantity != r.Quantity)
                || (r.WarehouseId.HasValue && existing.WarehouseId.HasValue && existing.WarehouseId != r.WarehouseId)
                || (r.LotId.HasValue && existing.LotId.HasValue && existing.LotId != r.LotId))
                throw new ConcurrencyException("WH_IDEMPOTENCY_CONFLICT",
                    $"The reference {reference} was already booked with a different quantity, warehouse or lot ({existing.Quantity}).");

            var inv = await uow.Inventories.GetByProductIdAsync(r.ProductId, ct);
            var now = inv?.TotalQuantity ?? 0;
            return new ProductionMovementResult(existing, true, now, now, existing.UnitCost ?? 0, existing.ValueDelta ?? 0,
                existing.StockDelta ?? 0, existing.WarehouseId, existing.LotId);
        }

        var inventory = await uow.Inventories.GetByProductIdAsync(r.ProductId, ct)
            ?? throw new EntityNotFoundException("Inventory", r.ProductId);
        var previous = inventory.TotalQuantity;

        InventoryLot? lot = null;
        if (r.LotId.HasValue)
        {
            lot = await uow.InventoryLots.GetByIdAsync(r.LotId.Value, ct);
            if (lot == null || lot.ProductId != r.ProductId)
                throw new BusinessRuleException("WH_LOT_NOT_FOUND", "The lot does not exist for this product.");
        }

        if (r.RequiresLot) AssertLotNamed(r);

        var target = await ledger.TargetAsync(r.WarehouseId ?? lot?.WarehouseId, null, ct);
        var policy = r.AllowNegative ? IssuePolicy.AllowNegative : IssuePolicy.Refuse;
        var quantity = r.Quantity;
        decimal unitCost, valueDelta, stockDelta;
        Guid? lotId = lot?.LotId;
        Guid? warehouseId = target.WarehouseId;

        switch (r.Type)
        {
            case MovementType.ProductionIssue when r.ReservationId.HasValue:
            {
                // issue against a production reservation: the reserved quantity leaves (partial allowed), the stock taken is valued lot by lot
                var reservation = await uow.Reservations.GetByIdAsync(r.ReservationId.Value, ct)
                    ?? throw new BusinessRuleException("WH_RESERVATION_NOT_FOUND", "The reservation does not exist.");
                if (reservation.Status != ReservationStatus.Reserved || reservation.SourceType != ReservationSource.ProductionOrder)
                    throw new BusinessRuleException("WH_RESERVATION_NOT_OPEN", "The reservation is not an open production reservation.");
                var item = reservation.ReservedItems.FirstOrDefault(i => i.ProductId == r.ProductId && (!r.SourceLineId.HasValue || i.SourceLineId == r.SourceLineId))
                    ?? throw new BusinessRuleException("WH_RESERVATION_LINE", "The reservation has no such line for this item.");

                // a lot-controlled item may only leave from lots: the recorded lot split (or the named lot) must cover the quantity
                if (r.RequiresLot && !r.LotId.HasValue && item.LotAllocations.Sum(a => a.Remaining) < quantity)
                    throw new BusinessRuleException("WH_LOT_REQUIRED", "A lot-controlled item can only be issued from lots: the reservation holds less in lots than the quantity asked.");

                var taken = await ledger.ConsumeAsync(inventory, item, quantity, r.LotId, ct);
                var lotValue = taken.Sum(t => t.Quantity * (inventory.ValuationMethod != ValuationMethod.Average && t.Lot.UnitCost > 0 ? t.Lot.UnitCost : inventory.Cost));
                var fromPool = quantity - taken.Sum(t => t.Quantity);
                var total = lotValue + fromPool * inventory.Cost;
                unitCost = quantity > 0 ? Math.Round(total / quantity, 6) : 0;
                valueDelta = -Math.Round(total, 2);
                stockDelta = -quantity;
                warehouseId = taken.Count == 1 ? taken[0].Lot.WarehouseId : item.WarehouseId;
                lotId = taken.Count == 1 ? taken[0].Lot.LotId : null;
                if (reservation.IsFullyConsumed()) reservation.Fulfill();
                await uow.Reservations.UpdateAsync(reservation, ct);
                break;
            }

            case MovementType.ProductionIssue:
            case MovementType.ProductionScrap:
                if (lot != null && r.Type == MovementType.ProductionIssue && !lot.IsUsable())
                    throw new BusinessRuleException("WH_LOT_NOT_USABLE", $"Lot {lot.LotNumber} is on {lot.Status}: it cannot be issued.");
                unitCost = IssueCost(inventory, lot);
                if (lot != null) await ledger.IssueFromLotAsync(inventory, lot, quantity, policy, ct);
                else await ledger.IssueAsync(inventory, quantity, target, policy, LotDepletion.None, ct);
                valueDelta = -Math.Round(quantity * unitCost, 2);
                stockDelta = -quantity;
                break;

            case MovementType.ProductionReturn:
                unitCost = r.UnitCost ?? inventory.Cost;
                if (lot != null) await ledger.ReturnToLotAsync(inventory, lot, quantity, unitCost, ct);
                else await ledger.ReceiveAsync(inventory, quantity, unitCost, target, null, ct);
                valueDelta = Math.Round(quantity * unitCost, 2);
                stockDelta = quantity;
                break;

            case MovementType.ProductionReceipt:
                unitCost = r.UnitCost ?? inventory.Cost;
                var created = await ledger.ReceiveAsync(inventory, quantity, unitCost, target,
                    string.IsNullOrWhiteSpace(r.LotNumber) ? null
                        : new NewLot(r.LotNumber.Trim(), unitCost, r.ExpiryDate, null, r.ManufactureDate, LotSource.Production), ct);
                if (created != null)
                {
                    if (r.InitialLotStatus is { } status && status != LotStatus.Available) await ledger.SetLotStatusAsync(created, status, ct);
                    lotId = created.LotId;
                }
                valueDelta = Math.Round(quantity * unitCost, 2);
                stockDelta = quantity;
                break;

            case MovementType.QcRelease:
                if (lot == null) throw new BusinessRuleException("WH_LOT_REQUIRED", "A quality release names the lot.");
                if (lot.Status == LotStatus.Available)
                    throw new BusinessRuleException("WH_LOT_NOT_ON_HOLD", $"Lot {lot.LotNumber} is already available.");
                await ledger.SetLotStatusAsync(lot, LotStatus.Available, ct);
                quantity = lot.RemainingQuantity;
                warehouseId = lot.WarehouseId;
                unitCost = lot.UnitCost;
                valueDelta = 0;
                stockDelta = 0;
                break;

            case MovementType.SubcontractTransfer:
                if (!r.ToWarehouseId.HasValue)
                    throw new BusinessRuleException("WH_WAREHOUSE_REQUIRED", "A subcontract transfer names the receiving warehouse.");
                if (target.WarehouseId == r.ToWarehouseId)
                    throw new BusinessRuleException("WH_WAREHOUSE_SAME", "The sending and the receiving warehouse are the same.");
                await ledger.MoveBetweenWarehousesAsync(r.ProductId, target.WarehouseId, r.ToWarehouseId, quantity, lot, ct);
                unitCost = inventory.Cost;
                valueDelta = 0;
                stockDelta = 0;
                break;

            default:
                throw new BusinessRuleException("WH_MOVEMENT_TYPE", $"{r.Type} is not a production movement.");
        }

        await uow.Inventories.UpdateAsync(inventory, ct);

        var movement = new StockMovement(Guid.Empty, r.ProductId, quantity, r.Type, null, null, reference, r.Reason, r.SourceOrderId)
            .Describe(warehouseId, lotId, unitCost, valueDelta, stockDelta);
        await uow.StockMovements.AddAsync(movement, ct);

        return new ProductionMovementResult(movement, false, previous, inventory.TotalQuantity, unitCost, valueDelta, stockDelta, warehouseId, lotId);
    }

    /// <summary>
    /// Lot-controlled items (flag passed by the caller): a receipt creates a lot (number required); a return, scrap or transfer names the lot;
    /// an issue names the lot unless it is taken from a reservation (checked where the reservation is read).
    /// </summary>
    private static void AssertLotNamed(ProductionMovementRequest r)
    {
        var missing = r.Type switch
        {
            MovementType.ProductionReceipt => string.IsNullOrWhiteSpace(r.LotNumber),
            MovementType.ProductionIssue => !r.ReservationId.HasValue && !r.LotId.HasValue,
            MovementType.ProductionReturn or MovementType.ProductionScrap or MovementType.SubcontractTransfer => !r.LotId.HasValue,
            _ => false
        };
        if (missing)
            throw new BusinessRuleException("WH_LOT_REQUIRED", $"{r.Type} of a lot-controlled item needs a lot ({(r.Type == MovementType.ProductionReceipt ? "lot number" : "lot id")}).");
    }

    /// <summary>Average valuation issues at the item's average cost; FIFO / LIFO at the cost of the lot taken from (else the item cost).</summary>
    private static decimal IssueCost(InventoryEntity inventory, InventoryLot? lot)
        => inventory.ValuationMethod != ValuationMethod.Average && lot != null && lot.UnitCost > 0 ? lot.UnitCost : inventory.Cost;
}

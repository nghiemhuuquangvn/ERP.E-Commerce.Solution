using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.StockDocuments;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Application.Services;

/// <summary>
/// Works out the stock lines of a backflush (BR-MAT-08, ADR Phase 3 decisions 2 and 3). It reads the Warehouse, so it refuses a shortage <b>before</b> anything is stored
/// (<c>MFG_MAT_008</c>) - the report is then not stored either. Same split as a manual issue: the part the order's reservation holds, then free stock first-expiry-first.
/// </summary>
public sealed class BackflushPlanner(IWarehouseStockClient stock, IProductLookupClient products, IUnitOfMeasureClient units)
{
    /// <summary>The materials of an operation that are consumed by backflush.</summary>
    public static List<ProductionOrderMaterial> TargetsOf(ProductionOrder order, ProductionOrderOperation op)
        => order.Materials.Where(m => m.IssueMethod == IssueMethod.Backflush && m.OperationSeq == op.OperationSeq).ToList();

    /// <summary>The materials consumed when finished goods are received: backflush materials that no operation owns (BR-FG-05).</summary>
    public static List<ProductionOrderMaterial> ReceiptTargetsOf(ProductionOrder order)
        => order.Materials.Where(m => m.IssueMethod == IssueMethod.Backflush && m.OperationSeq is null).ToList();

    /// <summary>The lines to store for a report that brings the operation's cumulative output to <paramref name="cumulativeOutput"/> (good + scrap, pending reports included).</summary>
    public Task<List<NewStockLine>> PlanAsync(ProductionOrder order, ProductionOrderOperation op, decimal cumulativeOutput, IReadOnlyList<StockFlowLine> flows,
        bool allowNegative, DateTime now, CancellationToken ct)
        => PlanAsync(order, TargetsOf(order, op), cumulativeOutput, flows, allowNegative, now, ct);

    /// <summary>The lines to take <paramref name="targets"/> out of stock for the cumulative output (an operation's good + scrap, or the main product received), pending consumption included.</summary>
    public async Task<List<NewStockLine>> PlanAsync(ProductionOrder order, IReadOnlyList<ProductionOrderMaterial> targets, decimal cumulativeOutput, IReadOnlyList<StockFlowLine> flows,
        bool allowNegative, DateTime now, CancellationToken ct)
    {
        var lines = new List<NewStockLine>();
        if (targets.Count == 0) return lines;

        var items = await MaterialDocuments.ProductsAsync(products, targets, ct);
        var decimals = (await units.LookupAsync(targets.Select(m => m.UomId).Distinct().ToList(), ct)).ToDictionary(u => u.Id, u => u.DecimalPlaces);
        var reservation = await stock.GetReservationAsync(order.Id, ct);
        var open = reservation is { Status: "Reserved" } ? reservation : null;

        foreach (var material in targets)
        {
            var pending = flows.Where(f => f.OrderMaterialId == material.Id && f.IsBackflushConsumption && f.DocumentStatus == StockDocumentStatus.Pending
                && f.DocumentMode == StockDocumentMode.Forward).Sum(f => f.Qty);
            var qty = BackflushCalculator.Quantity(cumulativeOutput, material.RequiredQty, order.Quantity, decimals.GetValueOrDefault(material.UomId, 6), material.ConsumedQty + pending);
            if (qty <= 0) continue;

            var item = items[material.ComponentItemId];
            var held = open?.Lines.Where(l => l.ProductId == material.ComponentItemId && l.SourceLineId == material.Id).ToList() ?? [];
            var ownRemaining = item.IsLotControlled ? held.SelectMany(h => h.Lots).Sum(l => l.Remaining) : held.Sum(h => h.Remaining);
            var reserved = Math.Min(qty, ownRemaining);
            var free = qty - reserved;
            if (reserved > 0)
                lines.Add(new NewStockLine(material.Id, material.LineNo, material.ComponentItemId, material.ComponentCode, material.WarehouseId, null, reserved, true, null));
            if (free <= 0) continue;

            var availability = await stock.GetAvailabilityAsync(material.ComponentItemId, material.WarehouseId, ct);
            var usable = availability.Balances
                .Where(b => b.WarehouseId == material.WarehouseId && (b.LotId == null || (b.LotStatus == "Available" && (b.ExpiryDate is null || b.ExpiryDate > now))))
                .Sum(b => Math.Max(b.AvailableQuantity, 0m));
            var capacity = Math.Min(usable, Math.Max(availability.AvailableQuantity, 0m));
            var fromStock = Math.Min(free, capacity);
            var shortage = free - fromStock;

            if (fromStock > 0)
            {
                if (item.IsLotControlled)
                {
                    var suggestion = await stock.SuggestLotsAsync(material.ComponentItemId, fromStock, material.WarehouseId, ct);
                    foreach (var lot in suggestion.Lots.Where(l => l.Take > 0))
                        lines.Add(new NewStockLine(material.Id, material.LineNo, material.ComponentItemId, material.ComponentCode, material.WarehouseId, lot.LotId, lot.Take, false, null));
                    shortage += suggestion.Lots.Sum(l => l.Take) < fromStock ? fromStock - suggestion.Lots.Sum(l => l.Take) : 0m;
                }
                else
                {
                    lines.Add(new NewStockLine(material.Id, material.LineNo, material.ComponentItemId, material.ComponentCode, material.WarehouseId, null, fromStock, false, null));
                }
            }

            if (shortage <= 0) continue;
            // TC-BF-005 / 006: refuse, or (when the plant allows it and the item has no lot to name) book the rest below zero and flag it
            MfgRules.ThrowIf(!allowNegative || item.IsLotControlled, MfgErrors.Mat008, ("item", material.ComponentCode), ("shortage", shortage));
            lines.Add(new NewStockLine(material.Id, material.LineNo, material.ComponentItemId, material.ComponentCode, material.WarehouseId, null, shortage, false, null, IsNegativeStock: true));
        }
        return lines;
    }
}

/// <summary>An operation that is Completed has no time left to run: closes the open time logs of every completed operation of the order (BR-OP-06, T3.7).</summary>
public sealed class OperationLogCloser(ITimeLogRepository logs, IPlantClock clock)
{
    public async Task CloseForCompletedAsync(ProductionOrder order, CancellationToken ct = default)
    {
        var completed = order.CompletedOperations.Select(o => o.Id).ToHashSet();
        if (completed.Count == 0) return;
        var now = clock.UtcNow;
        foreach (var log in (await logs.ListOpenForOrderAsync(order.Id, ct)).Where(l => l.IsOpen && completed.Contains(l.OrderOperationId)))
            log.Stop(now < log.StartAt ? log.StartAt : now);
    }
}

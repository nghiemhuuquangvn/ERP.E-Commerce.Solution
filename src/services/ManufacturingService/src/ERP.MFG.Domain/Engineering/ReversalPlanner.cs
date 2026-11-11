using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Domain.Engineering;

/// <summary>
/// Works out the lines of a Reversal document (P4-2, BR-OP-07, BR-FG-07). Pure: the only place the reversal lines are computed. A Posted document is undone line by line:
/// consumed material comes back into the lot(s) the Warehouse really used, at the cost booked on the issue; received finished goods leave through the lot the receipt created.
/// </summary>
public static class ReversalPlanner
{
    /// <summary>
    /// The lines that give back what a Posted backflush document (or the material part of a Posted receipt) consumed. One <c>ProductionReturn</c> per stored lot row; the part of a line
    /// that no lot covers (lot-less stock) goes back lot-less; a line without stored rows (posted before the rows were kept) goes back to <c>PostedLotId ?? LotId</c>.
    /// </summary>
    public static List<NewStockLine> ReturnMaterial(MaterialTransaction original)
    {
        var lines = new List<NewStockLine>();
        foreach (var line in original.MaterialLines.Where(l => l.Status == StockLineStatus.Posted))
        {
            var rows = line.Lots;
            if (rows.Count == 0)
            {
                lines.Add(Return(line, line.PostedLotId ?? line.LotId, line.WarehouseId, line.Qty, line.UnitCost));
                continue;
            }

            foreach (var row in rows)
                lines.Add(Return(line, row.LotId, row.WarehouseId ?? line.WarehouseId, row.Qty, row.UnitCost ?? line.UnitCost));
            var remainder = line.Qty - rows.Sum(r => r.Qty);
            if (remainder > 0) lines.Add(Return(line, null, line.WarehouseId, remainder, line.UnitCost));
        }
        return lines;
    }

    /// <summary>The lines that take the finished goods of a Posted receipt out of stock (they are never flagged as held: the lot they scrap is gone, so the reversal must not count as a lot waiting for inspection): <c>ProductionScrap</c> of the lot the receipt created (allowed on a lot on quality hold), else a lot-less issue.</summary>
    public static List<NewStockLine> TakeOutReceipt(MaterialTransaction original)
        => original.ReceiptLines.Where(l => l.Status == StockLineStatus.Posted)
            .Select(l => new NewStockLine(null, null, l.ItemId, l.ItemCode, l.WarehouseId, l.PostedLotId, l.Qty, false, l.UnitCost, false, StockLineKind.Receipt, l.OrderOutputId, l.OutputLineNo,
                l.LotNumber, l.ExpiryDate, l.ManufactureDate, false, l.PostedLotId is null ? ReversalAction.Issue : ReversalAction.Scrap, l.Id))
            .ToList();

    private static NewStockLine Return(MaterialTransactionLine line, Guid? lotId, Guid warehouseId, decimal qty, decimal? unitCost)
        => new(line.OrderMaterialId, line.MaterialLineNo, line.ItemId, line.ItemCode, warehouseId, lotId, qty, false, unitCost, false, StockLineKind.Material, null, null, null, null, null, false,
            ReversalAction.Return, line.Id);
}

/// <summary>The checks of a reversal that need no data access (BR-OP-07, BR-FG-07).</summary>
public static class ReversalChecks
{
    /// <summary>
    /// BR-OP-07: after the report's good quantity leaves operation N, what is left must still cover what was taken downstream (the next operation's input, or - for the last operation -
    /// the finished goods received or being received). <c>Remaining</c> is what would be left; <c>Used</c> what downstream has taken.
    /// </summary>
    public static (bool Covered, decimal Remaining, decimal Used) ReportCovered(decimal operationGood, decimal reversedGood, decimal downstreamUsed)
    {
        var remaining = operationGood - reversedGood;
        return (remaining >= downstreamUsed, remaining, downstreamUsed);
    }

    /// <summary>BR-FG-07: the lot of a receipt line must still be whole and free in the Warehouse. Returns the reason in words, or null when the line can be taken out.</summary>
    public static string? LotProblem(string lot, decimal lineQty, decimal onHand, decimal reserved)
    {
        if (onHand < lineQty) return $"lô {lot} chỉ còn {onHand:0.######} trên {lineQty:0.######} đã nhập (phần còn lại đã được dùng)";
        if (onHand - reserved < lineQty) return $"lô {lot} đang được giữ chỗ {reserved:0.######}, không còn đủ {lineQty:0.######} để đảo";
        return null;
    }
}

using ERP.WH.Domain.Aggregates.Inventory;

namespace ERP.WH.Application.Services;

/// <summary>
/// The one first-expiry-first rule (FEFO), used by the reservation and by the lot suggestion so they can never disagree:
/// only a lot that is Available (not on hold, not blocked), not expired and with free quantity may be taken; optionally only
/// the lots of one warehouse; earliest expiry first (no expiry last), then the oldest receipt.
/// </summary>
public static class LotPicker
{
    public static IEnumerable<InventoryLot> Candidates(IEnumerable<InventoryLot> lots, Guid? warehouseId, bool restrictToWarehouse, DateTime now)
        => lots
            .Where(l => l.IsUsable()
                        && !(l.ExpiryDate.HasValue && l.ExpiryDate.Value < now)
                        && l.AvailableQuantity > 0
                        && (!restrictToWarehouse || l.WarehouseId == warehouseId))
            .OrderBy(l => l.ExpiryDate ?? DateTime.MaxValue)
            .ThenBy(l => l.ReceivedDate);

    /// <summary>Walks the candidates and takes up to <paramref name="quantity"/>; the rest is the shortfall the lots cannot cover.</summary>
    public static (List<(InventoryLot Lot, decimal Take)> Picks, decimal Shortfall) Pick(
        IEnumerable<InventoryLot> lots, decimal quantity, Guid? warehouseId, bool restrictToWarehouse, DateTime now)
    {
        var picks = new List<(InventoryLot, decimal)>();
        var remaining = quantity;
        foreach (var lot in Candidates(lots, warehouseId, restrictToWarehouse, now))
        {
            if (remaining <= 0) break;
            var take = Math.Min(lot.AvailableQuantity, remaining);
            picks.Add((lot, take));
            remaining -= take;
        }
        return (picks, Math.Max(remaining, 0));
    }
}

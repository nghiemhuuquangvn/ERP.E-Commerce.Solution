namespace ERP.MFG.Application.Abstractions;

// ── Warehouse stock API (WareHouseService internal/v1/stock, service token, caller "manufacturing") ────────────
// Field names follow the Warehouse answers (camelCase JSON, enums as text). Writes are idempotent on the Warehouse side: a movement by its key
// "<document number>#<line>", a reservation by its source document, a release by state, a lot status by value.

/// <summary>One row of "how much is where": a warehouse and a lot (null = not attributed to a lot).</summary>
public sealed record StockBalanceInfo(Guid? WarehouseId, string? WarehouseCode, Guid? LotId, string? LotNumber, string? LotStatus, DateTime? ExpiryDate,
    decimal Quantity, decimal ReservedQuantity, decimal LockedQuantity, decimal AvailableQuantity);

public sealed record StockAvailability(Guid ProductId, decimal TotalQuantity, decimal ReservedQuantity, decimal LockedQuantity, decimal AvailableQuantity,
    decimal UnitCost, List<StockBalanceInfo> Balances);

public sealed record SuggestedLotInfo(Guid LotId, string LotNumber, Guid? WarehouseId, DateTime? ExpiryDate, decimal AvailableQuantity, decimal Take);

/// <summary>First-expiry-first picks for a quantity; <c>Shortfall</c> is what the lots cannot cover, <c>LotLessAvailable</c> the stock that is not in a lot.</summary>
public sealed record LotSuggestion(Guid ProductId, Guid? WarehouseId, decimal Quantity, List<SuggestedLotInfo> Lots, decimal Shortfall, decimal LotLessAvailable);

public sealed record StockReservationLine(Guid ProductId, decimal Quantity, Guid? SourceLineId = null, Guid? LotId = null);

public sealed record StockReservationLotInfo(Guid LotId, string? LotNumber, decimal Quantity, decimal Consumed, decimal Remaining);

public sealed record StockReservationLineResult(Guid ProductId, Guid? SourceLineId, Guid? WarehouseId, decimal Quantity, decimal Consumed, decimal Remaining,
    List<StockReservationLotInfo> Lots);

public sealed record StockReservation(Guid ReservationId, string SourceType, Guid SourceId, string? SourceReference, string Status, bool Replayed,
    List<StockReservationLineResult> Lines);

/// <summary>
/// One production movement. <c>IdempotencyKey</c> is "&lt;document number&gt;#&lt;line&gt;" (e.g. MO-2611-00001#3); booking it again answers the first result.
/// <c>RequiresLot</c> / <c>RequiresSerial</c> are the item's flags read from Product - the Warehouse enforces them.
/// </summary>
public sealed record StockMovementRequest(
    string MovementType,
    Guid ProductId,
    decimal Quantity,
    string IdempotencyKey,
    Guid? WarehouseId = null,
    Guid? ToWarehouseId = null,
    Guid? LotId = null,
    string? LotNumber = null,
    DateTime? ExpiryDate = null,
    DateTime? ManufactureDate = null,
    decimal? UnitCost = null,
    string? InitialLotStatus = null,
    bool AllowNegative = false,
    string? Reason = null,
    Guid? SourceOrderId = null,
    Guid? ReservationId = null,
    Guid? SourceLineId = null,
    bool RequiresLot = false,
    bool RequiresSerial = false);

public sealed record StockMovementResult(Guid MovementId, string MovementType, decimal Quantity, DateTime RecordedAt, decimal? UnitCost, decimal? ValueDelta,
    Guid? WarehouseId, Guid? LotId, bool Replayed);

public sealed record LotStatusResult(Guid LotId, string LotNumber, string Status, bool Unchanged);

public sealed record ReservationReleased(Guid ReservationId, string Status, DateTime ReleasedAt);

/// <summary>
/// The Warehouse half of production orders. No caching (live stock, and writes). Failures: Warehouse refusals (HTTP 400) come back as a
/// <c>BusinessRuleException</c> carrying the WH_* code; a payload that disagrees with an earlier use of the key is a <c>ConcurrencyException</c>
/// with WH_IDEMPOTENCY_CONFLICT (never retried); a plain optimistic-locking conflict is retried 3 times and then surfaced; an unreachable service is MFG_INT_001.
/// </summary>
public interface IWarehouseStockClient
{
    Task<StockAvailability> GetAvailabilityAsync(Guid productId, Guid? warehouseId, CancellationToken ct = default);

    Task<LotSuggestion> SuggestLotsAsync(Guid productId, decimal quantity, Guid? warehouseId, CancellationToken ct = default);

    /// <summary>Holds components for a production order in one warehouse; asking again for the same order answers the first reservation (<c>Replayed</c>).</summary>
    Task<StockReservation> ReserveAsync(Guid productionOrderId, string? orderNumber, Guid? warehouseId, IReadOnlyList<StockReservationLine> lines, CancellationToken ct = default);

    Task<StockReservation?> GetReservationAsync(Guid productionOrderId, CancellationToken ct = default);

    /// <summary>Frees what has not been consumed; releasing a reservation that is no longer open answers its state.</summary>
    Task<ReservationReleased> ReleaseReservationAsync(Guid reservationId, string? reason, CancellationToken ct = default);

    Task<StockMovementResult> RecordMovementAsync(StockMovementRequest request, CancellationToken ct = default);

    /// <summary>Puts a lot on "QcHold" or "Blocked"; the way back to Available is a QcRelease movement.</summary>
    Task<LotStatusResult> SetLotStatusAsync(Guid lotId, string status, CancellationToken ct = default);
}

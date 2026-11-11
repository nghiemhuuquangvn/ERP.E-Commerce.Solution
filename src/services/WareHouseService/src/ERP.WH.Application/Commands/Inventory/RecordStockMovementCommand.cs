using MediatR;

namespace ERP.WH.Application.Commands.Inventory;

public class RecordStockMovementCommand : IRequest<RecordStockMovementResponse>
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public required string MovementType { get; set; }
    public string? FromLocation { get; set; }
    public string? ToLocation { get; set; }
    public string? Reference { get; set; }
    public string? Reason { get; set; }
    public Guid? SourceOrderId { get; set; }

    // Production movements (W3): ProductionIssue, ProductionReturn, ProductionReceipt, ProductionScrap, QcRelease, SubcontractTransfer.
    // The reference is then required ("<document number>#<line>") and is the idempotency key.
    public Guid? WarehouseId { get; set; }
    public Guid? ToWarehouseId { get; set; }
    public Guid? LotId { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ManufactureDate { get; set; }
    public decimal? UnitCost { get; set; }
    /// <summary>"Available" (default), "QcHold" or "Blocked" - the status a received lot starts in.</summary>
    public string? InitialLotStatus { get; set; }
    public bool AllowNegative { get; set; }

    /// <summary>ProductionIssue only: take the stock out of this production reservation (partial allowed) instead of from free stock.</summary>
    public Guid? ReservationId { get; set; }
    /// <summary>The reservation line (production order component) to take from, when the item appears on more than one.</summary>
    public Guid? SourceLineId { get; set; }

    /// <summary>Production movements: the item is lot-controlled (flag read from Product by the caller; internal API only - the user endpoint leaves it unchecked).</summary>
    public bool RequiresLot { get; set; }
    /// <summary>Production movements: the item is serial-controlled; refused (WH_SERIAL_NOT_SUPPORTED) because per-serial tracking is not on the production path yet.</summary>
    public bool RequiresSerial { get; set; }

    /// <summary>ProductionReceipt of an item the Warehouse has never held: the inventory record is created with this name and SKU.</summary>
    public string? ProductName { get; set; }
    public string? ProductSku { get; set; }

    /// <summary>
    /// ProductionIssue only (W7, rework): the named lot may be on quality hold. A privilege of the internal service API: the user endpoint resets it to
    /// false before the command is sent, so a request body can never set it.
    /// </summary>
    public bool AllowHeldLot { get; set; }
}

/// <summary>One lot a production movement acted on (W7); <c>ParentLotId</c> is set for the sub-lot a split transfer created.</summary>
public class MovementLotDto
{
    public Guid LotId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ParentLotId { get; set; }
}

public class RecordStockMovementResponse
{
    public Guid MovementId { get; set; }
    public required string MovementType { get; set; }
    public decimal Quantity { get; set; }
    public DateTime RecordedAt { get; set; }

    // Production movements only (null for the older types)
    public decimal? UnitCost { get; set; }
    public decimal? ValueDelta { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? LotId { get; set; }
    /// <summary>
    /// The lots the movement really used (W7): one per lot taken by a reservation issue (<c>LotId</c> is null when there were several), the lot of
    /// a single-lot movement, the new sub-lot of a split transfer. Empty for lot-less stock and for movements booked before W7.
    /// </summary>
    public List<MovementLotDto> Lots { get; set; } = [];
    /// <summary>True when the same reference had been booked before: this is the first result, nothing changed.</summary>
    public bool Replayed { get; set; }
}

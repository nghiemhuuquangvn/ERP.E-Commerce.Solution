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
    /// <summary>True when the same reference had been booked before: this is the first result, nothing changed.</summary>
    public bool Replayed { get; set; }
}

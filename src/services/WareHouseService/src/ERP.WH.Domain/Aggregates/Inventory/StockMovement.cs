namespace ERP.WH.Domain.Aggregates.Inventory;

public enum MovementType
{
    Received = 0,
    Sold = 1,
    Adjusted = 2,
    Damaged = 3,
    Transferred = 4
}

public class StockMovement
{
    public Guid MovementId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public MovementType MovementType { get; private set; }
    public string? FromLocation { get; private set; }
    public string? ToLocation { get; private set; }
    public string? Reference { get; private set; }
    public string? Reason { get; private set; }
    public Guid? SourceOrderId { get; private set; }
    public DateTime RecordedAt { get; private set; }

    public StockMovement(
        Guid productId,
        int quantity,
        MovementType movementType,
        string? fromLocation,
        string? toLocation,
        string? reference,
        string? reason = null,
        Guid? sourceOrderId = null)
    {
        MovementId = Guid.NewGuid();
        ProductId = productId;
        Quantity = quantity;
        MovementType = movementType;
        FromLocation = fromLocation;
        ToLocation = toLocation;
        Reference = reference;
        Reason = reason;
        SourceOrderId = sourceOrderId;
        RecordedAt = DateTime.UtcNow;
    }
}

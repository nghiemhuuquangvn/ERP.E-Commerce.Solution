namespace ERP.WH.Domain.Aggregates.Inventory;

/// <summary>
/// Per-sale COGS audit trail — one row per lot (or one row using the Average-method
/// unit cost, for non-lot-tracked products) consumed by a shipment. Written only by
/// IReservationFulfillmentService at ship time; never mutated afterward (append-only,
/// same discipline as StockMovement).
/// </summary>
public class CostConsumptionLog
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? LotId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal TotalCost { get; private set; }
    public Guid SourceOrderId { get; private set; }
    public DateTime ConsumedAt { get; private set; }

    private CostConsumptionLog() { }

    public CostConsumptionLog(Guid productId, Guid? lotId, int quantity, decimal unitCost, Guid sourceOrderId)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        LotId = lotId;
        Quantity = quantity;
        UnitCost = unitCost;
        TotalCost = quantity * unitCost;
        SourceOrderId = sourceOrderId;
        ConsumedAt = DateTime.UtcNow;
    }
}

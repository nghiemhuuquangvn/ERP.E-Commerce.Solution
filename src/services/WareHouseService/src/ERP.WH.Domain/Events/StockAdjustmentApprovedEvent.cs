namespace ERP.WH.Domain.Events;

public class StockAdjustmentApprovedEvent
{
    public Guid AdjustmentId { get; set; }
    public Guid ProductId { get; set; }
    public int QuantityDelta { get; set; }
    public required string ApprovedBy { get; set; }
    public DateTime ApprovedAt { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

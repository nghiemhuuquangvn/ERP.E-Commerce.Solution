namespace ERP.WH.Domain.Events;

public class StockAdjustmentRejectedEvent
{
    public Guid AdjustmentId { get; set; }
    public Guid ProductId { get; set; }
    public required string RejectedBy { get; set; }
    public required string Reason { get; set; }
    public DateTime RejectedAt { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

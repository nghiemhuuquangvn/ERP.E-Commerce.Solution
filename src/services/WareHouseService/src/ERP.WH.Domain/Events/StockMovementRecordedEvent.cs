namespace ERP.WH.Domain.Events;

public class StockMovementRecordedEvent
{
    public Guid MovementId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string MovementType { get; set; }
    public string FromLocation { get; set; }
    public string ToLocation { get; set; }
    public string Reason { get; set; }
    public Guid? SourceOrderId { get; set; }
    public DateTime RecordedAt { get; set; }
    public string CorrelationId { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

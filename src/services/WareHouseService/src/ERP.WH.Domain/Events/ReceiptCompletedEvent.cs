namespace ERP.WH.Domain.Events;

public class ReceiptCompletedEvent
{
    public Guid ProductId { get; set; }
    public int QuantityReceived { get; set; }
    public string? LotNumber { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public string? Reference { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

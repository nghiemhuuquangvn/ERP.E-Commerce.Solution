namespace ERP.WH.Domain.Events;

public class TransferOrderCompletedEvent
{
    public Guid TransferOrderId { get; set; }
    public required string TransferNumber { get; set; }
    public Guid FromLocationId { get; set; }
    public Guid ToLocationId { get; set; }
    public DateTime CompletedAt { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

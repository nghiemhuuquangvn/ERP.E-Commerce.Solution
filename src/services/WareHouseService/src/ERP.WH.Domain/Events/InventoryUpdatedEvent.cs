namespace ERP.WH.Domain.Events;

public class InventoryUpdatedEvent
{
    public Guid ProductId { get; set; }
    public int PreviousQuantity { get; set; }
    public int CurrentQuantity { get; set; }
    public required string MovementType { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

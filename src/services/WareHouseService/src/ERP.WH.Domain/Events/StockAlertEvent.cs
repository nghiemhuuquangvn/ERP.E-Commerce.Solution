namespace ERP.WH.Domain.Events;

public class StockAlertEvent
{
    public Guid ProductId { get; set; }
    public required string ProductName { get; set; }
    public int CurrentQuantity { get; set; }
    public int ReorderPoint { get; set; }
    public int ReorderQuantity { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

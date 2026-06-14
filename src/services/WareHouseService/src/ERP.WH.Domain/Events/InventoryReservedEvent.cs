namespace ERP.WH.Domain.Events;

public class InventoryReservedEvent
{
    public Guid ReservationId { get; set; }
    public Guid OrderId { get; set; }
    public List<ReservedItemDto> ReservedItems { get; set; }
    public DateTime ReservedAt { get; set; }
    public string CorrelationId { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

public class ReservedItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string WarehouseLocation { get; set; }
}

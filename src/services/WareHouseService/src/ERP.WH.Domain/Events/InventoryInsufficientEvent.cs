namespace ERP.WH.Domain.Events;

/// <summary>
/// Published instead of throwing when a reservation request can't be fully satisfied.
/// Previously ReserveInventoryCommandHandler threw InvalidOperationException here,
/// which SalesOrderRabbitMqConsumer's catch block turned into an infinite
/// nack-with-requeue loop (the stock situation never changes on redelivery) — feeds
/// OrderFulfillmentSaga's compensation path instead.
/// </summary>
public class InventoryInsufficientEvent
{
    public Guid OrderId { get; set; }
    public List<InsufficientItemDto> InsufficientItems { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime EventCreatedAt { get; set; }
}

public class InsufficientItemDto
{
    public Guid ProductId { get; set; }
    public int RequestedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
}

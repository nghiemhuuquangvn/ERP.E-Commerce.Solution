using ERP.Shared.Core.Domain;

namespace ERP.WH.Domain.Events;

public class InventoryReservedEvent : ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid ReservationId { get; set; }
    public Guid OrderId { get; set; }
    public required List<ReservedItemDto> ReservedItems { get; set; }
    public DateTime ReservedAt { get; set; }
    public required string CorrelationId { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

public class ReservedItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public required string WarehouseLocation { get; set; }
}

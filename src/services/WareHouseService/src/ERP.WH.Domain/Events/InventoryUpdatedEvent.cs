using ERP.Shared.Core.Domain;

namespace ERP.WH.Domain.Events;

public class InventoryUpdatedEvent : ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid ProductId { get; set; }
    public decimal PreviousQuantity { get; set; }
    public decimal CurrentQuantity { get; set; }
    public required string MovementType { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

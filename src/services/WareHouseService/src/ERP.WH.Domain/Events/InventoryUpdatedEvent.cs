using ERP.Shared.Core.Domain;

namespace ERP.WH.Domain.Events;

public class InventoryUpdatedEvent : ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid ProductId { get; set; }
    public int PreviousQuantity { get; set; }
    public int CurrentQuantity { get; set; }
    public required string MovementType { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

using ERP.Shared.Core.Domain;

namespace ERP.WH.Domain.Events;

public class TransferOrderCompletedEvent : ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid TransferOrderId { get; set; }
    public required string TransferNumber { get; set; }
    public Guid FromLocationId { get; set; }
    public Guid ToLocationId { get; set; }
    public DateTime CompletedAt { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

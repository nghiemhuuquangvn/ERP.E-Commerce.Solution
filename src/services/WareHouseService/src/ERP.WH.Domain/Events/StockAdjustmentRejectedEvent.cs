using ERP.Shared.Core.Domain;

namespace ERP.WH.Domain.Events;

public class StockAdjustmentRejectedEvent : ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid AdjustmentId { get; set; }
    public Guid ProductId { get; set; }
    public required string RejectedBy { get; set; }
    public required string Reason { get; set; }
    public DateTime RejectedAt { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

using ERP.Shared.Core.Domain;

namespace ERP.WH.Domain.Events;

public class StockAdjustmentApprovedEvent : ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid AdjustmentId { get; set; }
    public Guid ProductId { get; set; }
    public int QuantityDelta { get; set; }
    public required string ApprovedBy { get; set; }
    public DateTime ApprovedAt { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

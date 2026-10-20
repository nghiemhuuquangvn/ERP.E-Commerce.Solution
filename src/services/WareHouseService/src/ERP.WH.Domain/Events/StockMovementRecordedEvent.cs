using ERP.Shared.Core.Domain;

namespace ERP.WH.Domain.Events;

public class StockMovementRecordedEvent : ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid MovementId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public required string MovementType { get; set; }
    public string? FromLocation { get; set; }
    public string? ToLocation { get; set; }
    public string? Reference { get; set; }
    public string? Reason { get; set; }
    public Guid? SourceOrderId { get; set; }
    public DateTime RecordedAt { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

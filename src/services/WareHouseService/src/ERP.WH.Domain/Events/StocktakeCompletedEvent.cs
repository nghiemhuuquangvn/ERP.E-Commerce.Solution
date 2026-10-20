using ERP.Shared.Core.Domain;

namespace ERP.WH.Domain.Events;

public class StocktakeCompletedEvent : ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid StocktakeId { get; set; }
    public required string StocktakeNumber { get; set; }
    public int LinesWithVariance { get; set; }
    public DateTime CompletedAt { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

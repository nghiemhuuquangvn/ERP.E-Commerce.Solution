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

    /// <summary>Cost per unit the movement was valued at (0 = unknown). ROADMAP P1-1b.</summary>
    public decimal UnitCost { get; set; }

    /// <summary>Signed change of the stock VALUE caused by this movement: positive when stock value goes up (receipt, gain),
    /// negative when it goes down (issue, loss), 0 when it does not change or the cost is unknown. AccountingService posts from
    /// this and <see cref="MovementType"/> only — <see cref="Quantity"/> has no consistent sign across movement sources.</summary>
    public decimal ValueDelta { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

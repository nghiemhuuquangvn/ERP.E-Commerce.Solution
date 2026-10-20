using ERP.Shared.Core.Domain;

namespace ERP.WH.Domain.Events;

public class ReceiptCompletedEvent : ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid ProductId { get; set; }
    public int QuantityReceived { get; set; }
    public string? LotNumber { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public string? Reference { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

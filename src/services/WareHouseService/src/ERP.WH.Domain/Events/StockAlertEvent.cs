using ERP.Shared.Core.Domain;

namespace ERP.WH.Domain.Events;

public class StockAlertEvent : ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid ProductId { get; set; }
    public required string ProductName { get; set; }
    public int CurrentQuantity { get; set; }
    public int ReorderPoint { get; set; }
    public int ReorderQuantity { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

using ERP.Shared.Core.Domain;

namespace ERP.WH.Domain.Events;

public class StockAlertEvent : ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid ProductId { get; set; }
    public required string ProductName { get; set; }
    public decimal CurrentQuantity { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal ReorderQuantity { get; set; }

    /// <summary>Warehouse SKU = the product code of purchase orders and RFQs (P1-9).</summary>
    public string? ProductSku { get; set; }
    /// <summary>Total minus reserved and locked: the figure the alert is decided on. Older senders left it 0.</summary>
    public decimal AvailableQuantity { get; set; }
    public decimal MinStock { get; set; }
    public decimal MaxStock { get; set; }
    public decimal SafetyStock { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

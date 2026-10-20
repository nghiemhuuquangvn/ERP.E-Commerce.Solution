using ERP.Shared.Core.Domain;

namespace ERP.WH.Domain.Events;

public class InventoryValuationSnapshotClosed : ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public Guid SnapshotId { get; set; }
    public required string PeriodCode { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime SnapshotDate { get; set; }
    public decimal TotalValue { get; set; }
    public int LineCount { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

namespace ERP.WH.Domain.Events;

public class InventoryValuationSnapshotClosed
{
    public Guid SnapshotId { get; set; }
    public required string PeriodCode { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime SnapshotDate { get; set; }
    public decimal TotalValue { get; set; }
    public int LineCount { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

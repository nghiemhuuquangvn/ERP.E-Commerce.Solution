namespace ERP.WH.Domain.Aggregates.Inventory;

public enum StockAdjustmentStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>
/// A requested stock change awaiting Warehouse Manager approval — per the business rule
/// that an adjustment must never touch Inventory directly. Previously there was no such
/// aggregate at all: AdjustStockCommand mutated Inventory synchronously with no request/
/// approval step.
/// </summary>
public class StockAdjustment
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? LocationId { get; private set; }
    public int QuantityDelta { get; private set; }
    public AdjustmentReason Reason { get; private set; }
    public string? Notes { get; private set; }
    public StockAdjustmentStatus Status { get; private set; }
    public string RequestedBy { get; private set; } = null!;
    public DateTime RequestedAt { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    private StockAdjustment() { }

    public StockAdjustment(Guid productId, int quantityDelta, AdjustmentReason reason, string requestedBy, Guid? locationId = null, string? notes = null)
    {
        if (quantityDelta == 0)
            throw new InvalidOperationException("Adjustment quantity delta must not be zero");

        Id = Guid.NewGuid();
        ProductId = productId;
        LocationId = locationId;
        QuantityDelta = quantityDelta;
        Reason = reason;
        Notes = notes;
        Status = StockAdjustmentStatus.Pending;
        RequestedBy = requestedBy;
        RequestedAt = DateTime.UtcNow;
    }

    public void Approve(string approvedBy)
    {
        if (Status != StockAdjustmentStatus.Pending)
            throw new InvalidOperationException($"Cannot approve adjustment in {Status} status");

        Status = StockAdjustmentStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAt = DateTime.UtcNow;
    }

    public void Reject(string rejectedBy, string reason)
    {
        if (Status != StockAdjustmentStatus.Pending)
            throw new InvalidOperationException($"Cannot reject adjustment in {Status} status");

        Status = StockAdjustmentStatus.Rejected;
        ApprovedBy = rejectedBy;
        ApprovedAt = DateTime.UtcNow;
        RejectionReason = reason;
    }
}

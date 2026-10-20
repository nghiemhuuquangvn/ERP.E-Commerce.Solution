using ERP.Shared.Core.Domain;
namespace ERP.WH.Domain.Aggregates.Stocktakes;

public enum StocktakeStatus
{
    InProgress = 0,
    Completed = 1,
    Cancelled = 2,
    /// <summary>Counting locked, variances computed, awaiting Warehouse Manager approval before Inventory is touched. Added after InProgress/Completed/Cancelled to not disturb their existing numeric values.</summary>
    PendingApproval = 3
}

public class StocktakeLine
{
    public Guid LineId { get; private set; }
    public Guid StocktakeId { get; private set; }
    public Guid ProductId { get; private set; }
    public int SystemQuantity { get; private set; }
    public int? CountedQuantity { get; private set; }
    public string? VarianceReason { get; private set; }
    public int Variance => (CountedQuantity ?? SystemQuantity) - SystemQuantity;

    private StocktakeLine() { }

    public StocktakeLine(Guid stocktakeId, Guid productId, int systemQuantity)
    {
        LineId = Guid.NewGuid();
        StocktakeId = stocktakeId;
        ProductId = productId;
        SystemQuantity = systemQuantity;
    }

    public void RecordCount(int countedQuantity, string? varianceReason = null)
    {
        CountedQuantity = countedQuantity;
        VarianceReason = varianceReason;
    }
}

public class Stocktake : ITenantOwned
{
    public Guid StocktakeId { get; private set; }
    /// <summary>Owning tenant — stamped by WarehouseDbContext on insert (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public string StocktakeNumber { get; private set; } = null!;
    public Guid? LocationId { get; private set; }
    public StocktakeStatus Status { get; private set; }
    public List<StocktakeLine> Lines { get; private set; } = new();
    public string? Notes { get; private set; }
    public string StartedBy { get; private set; } = null!;
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private Stocktake() { }

    public Stocktake(string stocktakeNumber, string startedBy, Guid? locationId = null, string? notes = null)
    {
        StocktakeId = Guid.NewGuid();
        StocktakeNumber = stocktakeNumber;
        StartedBy = startedBy;
        LocationId = locationId;
        Notes = notes;
        Status = StocktakeStatus.InProgress;
        StartedAt = DateTime.UtcNow;
    }

    public void AddLine(Guid productId, int systemQuantity)
    {
        if (Status != StocktakeStatus.InProgress)
            throw new InvalidOperationException("Cannot add lines to a completed or cancelled stocktake");

        Lines.Add(new StocktakeLine(StocktakeId, productId, systemQuantity));
    }

    public void RecordCount(Guid productId, int countedQuantity, string? varianceReason = null)
    {
        if (Status != StocktakeStatus.InProgress)
            throw new InvalidOperationException("Cannot record counts on a completed or cancelled stocktake");

        var line = Lines.FirstOrDefault(l => l.ProductId == productId)
            ?? throw new InvalidOperationException($"Product {productId} is not part of this stocktake");

        line.RecordCount(countedQuantity, varianceReason);
    }

    /// <summary>Locks counting and computes variances — does NOT touch Inventory. A Warehouse Manager must call Approve() before the variance actually posts.</summary>
    public List<StocktakeLine> Complete()
    {
        if (Status != StocktakeStatus.InProgress)
            throw new InvalidOperationException($"Cannot complete stocktake in status {Status}");

        var variances = Lines.Where(l => l.CountedQuantity.HasValue && l.Variance != 0).ToList();

        Status = StocktakeStatus.PendingApproval;

        return variances;
    }

    /// <summary>Only this step actually posts variance to Inventory (in the application handler) — matches the same approval-gate rule as StockAdjustment.</summary>
    public List<StocktakeLine> Approve()
    {
        if (Status != StocktakeStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot approve stocktake in status {Status}");

        var variances = Lines.Where(l => l.CountedQuantity.HasValue && l.Variance != 0).ToList();

        Status = StocktakeStatus.Completed;
        CompletedAt = DateTime.UtcNow;

        return variances;
    }

    public void Cancel()
    {
        if (Status == StocktakeStatus.Completed)
            throw new InvalidOperationException("Cannot cancel a completed stocktake");

        Status = StocktakeStatus.Cancelled;
    }
}

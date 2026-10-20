using ERP.Shared.Core.Domain;
namespace ERP.WH.Domain.Aggregates.Inventory;

public enum ReservationStatus
{
    Reserved = 0,
    Fulfilled = 1,
    Released = 2,
    Expired = 3
}

/// <summary>One lot's share of a reservation line, in units — a line can span multiple lots.</summary>
public class LotAllocation
{
    public Guid LotId { get; set; }
    public int Quantity { get; set; }
}

public class ReservedItem
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string WarehouseLocation { get; set; } = null!;
    /// <summary>Every lot (and quantity) actually allocated by FEFO picking, if the product is
    /// lot-tracked — empty for non-lot-tracked products. Replaces a previous single nullable
    /// LotId that only remembered the first lot touched, which made it impossible to correctly
    /// reverse a reservation spanning more than one lot.</summary>
    public List<LotAllocation> LotAllocations { get; set; } = new();
}

public class Reservation : ITenantOwned
{
    public Guid ReservationId { get; private set; }
    /// <summary>Owning tenant — stamped by WarehouseDbContext on insert (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public Guid OrderId { get; private set; }
    public List<ReservedItem> ReservedItems { get; private set; } = null!;
    public ReservationStatus Status { get; private set; }
    public DateTime HoldUntil { get; private set; }
    public DateTime ReservedAt { get; private set; }
    public DateTime? ReleasedAt { get; private set; }
    public string? ReleaseReason { get; private set; }

    // Constructor parameter "items" does not name-match the ReservedItems property,
    // so EF Core's constructor-binding convention cannot resolve it; this
    // parameterless constructor makes EF use property-setter materialization instead.
    private Reservation() { }

    public Reservation(Guid orderId, List<ReservedItem> items, DateTime holdUntil)
    {
        ReservationId = Guid.NewGuid();
        OrderId = orderId;
        ReservedItems = items ?? new List<ReservedItem>();
        Status = ReservationStatus.Reserved;
        HoldUntil = holdUntil;
        ReservedAt = DateTime.UtcNow;
    }

    public void Release(string? reason = null)
    {
        Status = ReservationStatus.Released;
        ReleasedAt = DateTime.UtcNow;
        ReleaseReason = reason;
    }

    public void Fulfill()
    {
        Status = ReservationStatus.Fulfilled;
    }

    public void Expire()
    {
        if (Status == ReservationStatus.Reserved)
        {
            Status = ReservationStatus.Expired;
            ReleasedAt = DateTime.UtcNow;
        }
    }

    public bool IsExpired() => DateTime.UtcNow > HoldUntil && Status == ReservationStatus.Reserved;
}

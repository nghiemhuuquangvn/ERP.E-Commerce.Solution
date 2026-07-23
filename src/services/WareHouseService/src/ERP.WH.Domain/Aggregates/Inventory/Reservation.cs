namespace ERP.WH.Domain.Aggregates.Inventory;

public enum ReservationStatus
{
    Reserved = 0,
    Fulfilled = 1,
    Released = 2,
    Expired = 3
}

public class ReservedItem
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string WarehouseLocation { get; set; } = null!;
}

public class Reservation
{
    public Guid ReservationId { get; private set; }
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

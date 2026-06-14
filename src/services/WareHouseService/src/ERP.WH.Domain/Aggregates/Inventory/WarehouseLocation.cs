namespace ERP.WH.Domain.Aggregates.Inventory;

public class WarehouseLocation
{
    public Guid LocationId { get; private set; }
    public string LocationCode { get; private set; }
    public string Aisle { get; private set; }
    public string Bin { get; private set; }
    public string Shelf { get; private set; }
    public int Capacity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public WarehouseLocation(
        string locationCode,
        string aisle,
        string bin,
        string shelf,
        int capacity)
    {
        LocationId = Guid.NewGuid();
        LocationCode = locationCode;
        Aisle = aisle;
        Bin = bin;
        Shelf = shelf;
        Capacity = capacity;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}

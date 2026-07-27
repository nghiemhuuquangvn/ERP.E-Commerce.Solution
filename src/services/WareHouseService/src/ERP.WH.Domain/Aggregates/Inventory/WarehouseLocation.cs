namespace ERP.WH.Domain.Aggregates.Inventory;

public class WarehouseLocation
{
    public Guid LocationId { get; private set; }
    /// <summary>Which physical site this location belongs to — nullable since existing locations predate the Warehouse entity; migration backfills them onto a default "Main Warehouse".</summary>
    public Guid? WarehouseId { get; private set; }
    public string LocationCode { get; private set; }
    public string? Name { get; private set; }
    public string? Zone { get; private set; }
    public string Aisle { get; private set; }
    public string Bin { get; private set; }
    public string Shelf { get; private set; }
    public int Capacity { get; private set; }
    public int CurrentOccupancy { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public WarehouseLocation(
        string locationCode,
        string aisle,
        string bin,
        string shelf,
        int capacity,
        string? name = null,
        string? zone = null)
    {
        LocationId = Guid.NewGuid();
        LocationCode = locationCode;
        Name = name;
        Zone = zone;
        Aisle = aisle;
        Bin = bin;
        Shelf = shelf;
        Capacity = capacity;
        CurrentOccupancy = 0;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignWarehouse(Guid? warehouseId)
    {
        WarehouseId = warehouseId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string? name, string? zone, string aisle, string bin, string shelf, int capacity)
    {
        Name = name;
        Zone = zone;
        Aisle = aisle;
        Bin = bin;
        Shelf = shelf;
        Capacity = capacity;
        UpdatedAt = DateTime.UtcNow;
    }
}

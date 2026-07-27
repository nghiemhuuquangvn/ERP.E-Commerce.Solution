namespace ERP.WH.Domain.Aggregates.Location;

/// <summary>
/// Physical site/branch — did not exist anywhere in this system before (the business doc
/// assumed it did). Deliberately minimal: only a parent for WarehouseLocation, not a
/// deeper Zone/Aisle/Rack/Bin hierarchy — that's a future item if multi-site actually
/// needs it.
/// </summary>
public class Warehouse
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Address { get; private set; }
    public bool IsActive { get; private set; }

    private Warehouse() { }

    public Warehouse(string code, string name, string? address = null)
    {
        Id = Guid.NewGuid();
        Code = code;
        Name = name;
        Address = address;
        IsActive = true;
    }

    public void Update(string name, string? address)
    {
        Name = name;
        Address = address;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}

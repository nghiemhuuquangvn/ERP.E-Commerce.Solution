using ERP.Shared.Core.Domain;

namespace ERP.POS.Domain.Entities;

public class Terminal : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public Guid? LocationId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? LastSyncAt { get; private set; }

    private Terminal() { }

    public static Terminal Create(string name, Guid? locationId = null)
    {
        return new Terminal
        {
            Id = Guid.NewGuid(),
            Name = name,
            LocationId = locationId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Rename(string name)
    {
        Name = name;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordSync()
    {
        LastSyncAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}

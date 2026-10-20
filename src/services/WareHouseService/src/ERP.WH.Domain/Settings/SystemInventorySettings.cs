namespace ERP.WH.Domain.Settings;

/// <summary>
/// Platform-wide Inventory toggles. Mirrors IdentityService's SystemAuthSettings
/// pattern, but declared standalone (no shared BaseEntity) — ERP.WH.Domain has no
/// project reference to ERP.Shared.Core, consistent with this service's existing
/// entities (e.g. Inventory), which are also plain POCOs.
/// </summary>
public class SystemInventorySettings : ERP.Shared.Core.Domain.IPlatformWideEntity
{
    public static readonly Guid WellKnownId = Guid.Parse("70000000-0000-0000-0000-000000000001");

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; } = Guid.Empty;
    public bool NegativeStockAllowed { get; private set; } = false;
    public string? CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    protected SystemInventorySettings() { }

    public static SystemInventorySettings CreateDefault()
    {
        return new SystemInventorySettings
        {
            Id = WellKnownId,
            NegativeStockAllowed = false,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(bool negativeStockAllowed)
    {
        NegativeStockAllowed = negativeStockAllowed;
        UpdatedAt = DateTime.UtcNow;
    }
}

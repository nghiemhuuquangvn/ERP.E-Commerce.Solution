namespace ERP.WH.Domain.Settings;

/// <summary>Per-tenant Inventory configuration. One row per TenantId (unique). No shared BaseEntity — see SystemInventorySettings.</summary>
public class TenantInventorySettings : ERP.Shared.Core.Domain.ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public int LowStockThreshold { get; private set; } = 10;
    public bool BackorderAllowed { get; private set; } = false;
    public int DefaultHoldDays { get; private set; } = 7;
    public string? CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    protected TenantInventorySettings() { }

    public static TenantInventorySettings CreateDefault(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required", nameof(tenantId));

        return new TenantInventorySettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LowStockThreshold = 10,
            BackorderAllowed = false,
            DefaultHoldDays = 7,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(int lowStockThreshold, bool backorderAllowed, int defaultHoldDays)
    {
        LowStockThreshold = lowStockThreshold;
        BackorderAllowed = backorderAllowed;
        DefaultHoldDays = defaultHoldDays;
        UpdatedAt = DateTime.UtcNow;
    }
}

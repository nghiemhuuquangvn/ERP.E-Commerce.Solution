using ERP.Shared.Core.Domain;

namespace ERP.SO.Domain.Entities.Settings;

/// <summary>Per-tenant Order configuration. One row per TenantId (unique).</summary>
public class TenantOrderSettings : BaseEntity
{
    public string OrderNumberPrefix { get; private set; } = "SO-";
    public int AutoCancelUnpaidAfterHours { get; private set; } = 24;
    public bool AllowEditAfterSubmit { get; private set; } = false;

    protected TenantOrderSettings() { }

    public static TenantOrderSettings CreateDefault(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required", nameof(tenantId));

        return new TenantOrderSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumberPrefix = "SO-",
            AutoCancelUnpaidAfterHours = 24,
            AllowEditAfterSubmit = false,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string orderNumberPrefix, int autoCancelUnpaidAfterHours, bool allowEditAfterSubmit)
    {
        OrderNumberPrefix = orderNumberPrefix;
        AutoCancelUnpaidAfterHours = autoCancelUnpaidAfterHours;
        AllowEditAfterSubmit = allowEditAfterSubmit;
    }
}

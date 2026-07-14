using ERP.Shared.Core.Domain;

namespace ERP.NTF.Domain.Entities.Settings;

/// <summary>Per-tenant default channel enablement. One row per TenantId (unique).</summary>
public class TenantNotificationSettings : BaseEntity
{
    public bool EmailEnabledByDefault { get; private set; } = true;
    public bool SmsEnabledByDefault { get; private set; } = false;
    public bool PushEnabledByDefault { get; private set; } = false;

    protected TenantNotificationSettings() { }

    public static TenantNotificationSettings CreateDefault(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required", nameof(tenantId));

        return new TenantNotificationSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmailEnabledByDefault = true,
            SmsEnabledByDefault = false,
            PushEnabledByDefault = false,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(bool emailEnabledByDefault, bool smsEnabledByDefault, bool pushEnabledByDefault)
    {
        EmailEnabledByDefault = emailEnabledByDefault;
        SmsEnabledByDefault = smsEnabledByDefault;
        PushEnabledByDefault = pushEnabledByDefault;
    }
}

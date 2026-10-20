using ERP.Shared.Core.Domain;

namespace ERP.NTF.Domain.Entities.Settings;

/// <summary>
/// Platform-wide channel availability toggles. Distinct from NotificationPreference
/// (per-user opt-in/out) — this is the tenant-admin-configured default, mirroring
/// PaymentService's System/Tenant Settings shape from Phase 1.
/// </summary>
public class SystemNotificationSettings : BaseEntity, IPlatformWideEntity
{
    public static readonly Guid WellKnownId = Guid.Parse("D0000000-0000-0000-0000-000000000001");

    public bool EmailChannelAvailable { get; private set; } = true;
    public bool SmsChannelAvailable { get; private set; } = true;
    public bool PushChannelAvailable { get; private set; } = true;

    protected SystemNotificationSettings() { }

    public static SystemNotificationSettings CreateDefault()
    {
        return new SystemNotificationSettings
        {
            Id = WellKnownId,
            EmailChannelAvailable = true,
            SmsChannelAvailable = true,
            PushChannelAvailable = true,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(bool emailChannelAvailable, bool smsChannelAvailable, bool pushChannelAvailable)
    {
        EmailChannelAvailable = emailChannelAvailable;
        SmsChannelAvailable = smsChannelAvailable;
        PushChannelAvailable = pushChannelAvailable;
    }
}

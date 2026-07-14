using ERP.Shared.Core.Domain;

namespace ERP.NTF.Domain.Entities.Settings;

/// <summary>Platform-wide toggle for whether tenants may configure their own SMTP.</summary>
public class SystemEmailSettings : BaseEntity
{
    public static readonly Guid WellKnownId = Guid.Parse("D0000000-0000-0000-0000-000000000002");

    public bool CustomSmtpAllowed { get; private set; } = true;

    protected SystemEmailSettings() { }

    public static SystemEmailSettings CreateDefault()
    {
        return new SystemEmailSettings
        {
            Id = WellKnownId,
            CustomSmtpAllowed = true,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(bool customSmtpAllowed)
    {
        CustomSmtpAllowed = customSmtpAllowed;
    }
}

using ERP.Shared.Core.Domain;

namespace ERP.NTF.Domain.Entities.Settings;

/// <summary>Platform-wide toggle for whether tenants may configure their own SMS gateway.</summary>
public class SystemSmsSettings : BaseEntity
{
    public static readonly Guid WellKnownId = Guid.Parse("D0000000-0000-0000-0000-000000000003");

    public bool CustomSmsGatewayAllowed { get; private set; } = true;

    protected SystemSmsSettings() { }

    public static SystemSmsSettings CreateDefault()
    {
        return new SystemSmsSettings
        {
            Id = WellKnownId,
            CustomSmsGatewayAllowed = true,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(bool customSmsGatewayAllowed)
    {
        CustomSmsGatewayAllowed = customSmsGatewayAllowed;
    }
}

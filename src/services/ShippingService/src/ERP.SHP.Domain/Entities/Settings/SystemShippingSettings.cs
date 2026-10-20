using ERP.Shared.Core.Domain;

namespace ERP.SHP.Domain.Entities.Settings;

/// <summary>Platform-wide carrier availability toggles. Mirrors IdentityService's SystemAuthSettings.</summary>
public class SystemShippingSettings : BaseEntity, IPlatformWideEntity
{
    public static readonly Guid WellKnownId = Guid.Parse("90000000-0000-0000-0000-000000000001");

    public bool CustomCarriersAllowed { get; private set; } = true;

    protected SystemShippingSettings() { }

    public static SystemShippingSettings CreateDefault()
    {
        return new SystemShippingSettings
        {
            Id = WellKnownId,
            CustomCarriersAllowed = true,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(bool customCarriersAllowed)
    {
        CustomCarriersAllowed = customCarriersAllowed;
    }
}

using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities.Settings;

/// <summary>Platform-wide Tax configuration toggles. Mirrors IdentityService's SystemAuthSettings.</summary>
public class SystemTaxSettings : BaseEntity
{
    public static readonly Guid WellKnownId = Guid.Parse("A0000000-0000-0000-0000-000000000001");

    public bool RegionalTaxConfigAllowed { get; private set; } = true;

    protected SystemTaxSettings() { }

    public static SystemTaxSettings CreateDefault()
    {
        return new SystemTaxSettings
        {
            Id = WellKnownId,
            RegionalTaxConfigAllowed = true,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(bool regionalTaxConfigAllowed)
    {
        RegionalTaxConfigAllowed = regionalTaxConfigAllowed;
    }
}

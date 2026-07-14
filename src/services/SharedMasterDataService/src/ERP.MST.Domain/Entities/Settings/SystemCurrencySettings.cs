using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities.Settings;

/// <summary>Platform-wide currency availability toggles. Mirrors IdentityService's SystemAuthSettings.</summary>
public class SystemCurrencySettings : BaseEntity
{
    public static readonly Guid WellKnownId = Guid.Parse("B0000000-0000-0000-0000-000000000001");

    public bool MultiCurrencyAllowed { get; private set; } = true;

    protected SystemCurrencySettings() { }

    public static SystemCurrencySettings CreateDefault()
    {
        return new SystemCurrencySettings
        {
            Id = WellKnownId,
            MultiCurrencyAllowed = true,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(bool multiCurrencyAllowed)
    {
        MultiCurrencyAllowed = multiCurrencyAllowed;
    }
}

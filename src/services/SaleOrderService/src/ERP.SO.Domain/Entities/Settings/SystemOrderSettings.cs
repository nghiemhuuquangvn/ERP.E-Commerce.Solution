using ERP.Shared.Core.Domain;

namespace ERP.SO.Domain.Entities.Settings;

/// <summary>Platform-wide Order toggles. Mirrors IdentityService's SystemAuthSettings.</summary>
public class SystemOrderSettings : BaseEntity, IPlatformWideEntity
{
    public static readonly Guid WellKnownId = Guid.Parse("60000000-0000-0000-0000-000000000002");

    public bool CustomOrderNumberFormatAllowed { get; private set; } = true;

    protected SystemOrderSettings() { }

    public static SystemOrderSettings CreateDefault()
    {
        return new SystemOrderSettings
        {
            Id = WellKnownId,
            CustomOrderNumberFormatAllowed = true,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(bool customOrderNumberFormatAllowed)
    {
        CustomOrderNumberFormatAllowed = customOrderNumberFormatAllowed;
    }
}

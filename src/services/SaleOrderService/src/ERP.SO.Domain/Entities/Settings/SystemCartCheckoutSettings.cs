using ERP.Shared.Core.Domain;

namespace ERP.SO.Domain.Entities.Settings;

/// <summary>Platform-wide Cart/Checkout/Customer toggles. Mirrors IdentityService's SystemAuthSettings.</summary>
public class SystemCartCheckoutSettings : BaseEntity
{
    public static readonly Guid WellKnownId = Guid.Parse("60000000-0000-0000-0000-000000000001");

    public bool GuestCheckoutAllowed { get; private set; } = true;

    protected SystemCartCheckoutSettings() { }

    public static SystemCartCheckoutSettings CreateDefault()
    {
        return new SystemCartCheckoutSettings
        {
            Id = WellKnownId,
            GuestCheckoutAllowed = true,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(bool guestCheckoutAllowed)
    {
        GuestCheckoutAllowed = guestCheckoutAllowed;
    }
}

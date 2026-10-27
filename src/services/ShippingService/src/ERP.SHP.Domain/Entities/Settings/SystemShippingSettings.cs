using ERP.Shared.Core.Domain;

namespace ERP.SHP.Domain.Entities.Settings;

/// <summary>Platform-wide carrier availability toggles. Mirrors IdentityService's SystemAuthSettings.</summary>
public class SystemShippingSettings : BaseEntity, IPlatformWideEntity
{
    public static readonly Guid WellKnownId = Guid.Parse("90000000-0000-0000-0000-000000000001");

    public bool CustomCarriersAllowed { get; private set; } = true;

    // P1-11: the platform switches each carrier integration on or off for every shop (a shop still needs its own account).
    public bool GhtkAvailable { get; private set; } = true;
    public bool VtpAvailable { get; private set; } = true;
    public bool GhnAvailable { get; private set; } = true;
    public bool JntAvailable { get; private set; } = true;

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

    public void UpdateCarriers(bool ghtk, bool vtp, bool ghn, bool jnt)
    {
        GhtkAvailable = ghtk;
        VtpAvailable = vtp;
        GhnAvailable = ghn;
        JntAvailable = jnt;
    }

    public bool IsAvailable(string carrierCode) => carrierCode switch
    {
        CarrierCodes.Ghtk => GhtkAvailable,
        CarrierCodes.ViettelPost => VtpAvailable,
        CarrierCodes.Ghn => GhnAvailable,
        CarrierCodes.JAndT => JntAvailable,
        _ => false
    };
}

using ERP.Shared.Core.Domain;

namespace ERP.PAY.Domain.Entities.Settings;

/// <summary>Platform-wide gateway availability toggles. Mirrors IdentityService's SystemAuthSettings.</summary>
public class SystemPaymentSettings : BaseEntity, IPlatformWideEntity
{
    public static readonly Guid WellKnownId = Guid.Parse("80000000-0000-0000-0000-000000000001");

    public bool VnPayAvailable { get; private set; } = true;
    public bool ManualBankTransferAvailable { get; private set; } = true;
    public bool MoMoAvailable { get; private set; } = true;
    public bool ZaloPayAvailable { get; private set; } = true;

    protected SystemPaymentSettings() { }

    public static SystemPaymentSettings CreateDefault()
    {
        return new SystemPaymentSettings
        {
            Id = WellKnownId,
            VnPayAvailable = true,
            ManualBankTransferAvailable = true,
            MoMoAvailable = true,
            ZaloPayAvailable = true,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(bool vnPayAvailable, bool manualBankTransferAvailable, bool moMoAvailable = true, bool zaloPayAvailable = true)
    {
        VnPayAvailable = vnPayAvailable;
        ManualBankTransferAvailable = manualBankTransferAvailable;
        MoMoAvailable = moMoAvailable;
        ZaloPayAvailable = zaloPayAvailable;
    }
}

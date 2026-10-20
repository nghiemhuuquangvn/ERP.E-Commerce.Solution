using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Entities.Settings;

/// <summary>
/// Single well-known-GUID row holding platform-wide Store/Product setting toggles.
/// Mirrors IdentityService's SystemAuthSettings (System/Tenant two-tier pattern).
/// </summary>
public class SystemStoreProductSettings : BaseEntity, IPlatformWideEntity
{
    public static readonly Guid WellKnownId = Guid.Parse("50000000-0000-0000-0000-000000000001");

    public bool PriceDisplayIncludesTaxByDefault { get; private set; } = true;
    public bool ProductReviewsEnabled { get; private set; } = true;

    protected SystemStoreProductSettings() { }

    public static SystemStoreProductSettings CreateDefault()
    {
        return new SystemStoreProductSettings
        {
            Id = WellKnownId,
            PriceDisplayIncludesTaxByDefault = true,
            ProductReviewsEnabled = true,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(bool priceDisplayIncludesTaxByDefault, bool productReviewsEnabled)
    {
        PriceDisplayIncludesTaxByDefault = priceDisplayIncludesTaxByDefault;
        ProductReviewsEnabled = productReviewsEnabled;
    }
}

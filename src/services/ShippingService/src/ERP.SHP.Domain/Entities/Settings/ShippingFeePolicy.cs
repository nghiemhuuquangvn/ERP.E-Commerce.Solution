namespace ERP.SHP.Domain.Entities.Settings;

/// <summary>
/// What the customer pays for delivery (ROADMAP P1-11b): the shop's flat fee, free once the goods reach the shop's threshold.
/// The value is the goods after any coupon discount; a threshold of zero means "never free". The fee is the shop's own price list,
/// not the carrier's cost: the difference stays with the shop.
/// </summary>
public static class ShippingFeePolicy
{
    public static decimal Compute(decimal defaultShippingFee, decimal freeShippingThreshold, decimal merchandiseValue)
    {
        if (defaultShippingFee <= 0) return 0m;
        if (freeShippingThreshold > 0 && merchandiseValue >= freeShippingThreshold) return 0m;
        return defaultShippingFee;
    }

    public static decimal Compute(TenantShippingSettings? settings, decimal merchandiseValue) =>
        settings == null ? 0m : Compute(settings.DefaultShippingFee, settings.FreeShippingThreshold, merchandiseValue);
}

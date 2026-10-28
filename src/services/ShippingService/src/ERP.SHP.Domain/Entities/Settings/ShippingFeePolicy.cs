using ERP.SHP.Domain.Entities;

namespace ERP.SHP.Domain.Entities.Settings;

/// <summary>
/// What the customer pays for delivery (ROADMAP P1-11b / P1-11c): the first matching row of the shop's price list (province x weight band), else the shop's
/// flat fee; then free once the goods reach the shop's threshold. The value is the goods after any coupon discount; a threshold of zero means "never free".
/// The fee is the shop's own price, not the carrier's cost: the difference stays with the shop.
/// </summary>
public static class ShippingFeePolicy
{
    /// <summary>The flat fee only (P1-11b): no price list, no province, no weight.</summary>
    public static decimal Compute(decimal defaultShippingFee, decimal freeShippingThreshold, decimal merchandiseValue)
    {
        if (defaultShippingFee <= 0) return 0m;
        if (freeShippingThreshold > 0 && merchandiseValue >= freeShippingThreshold) return 0m;
        return defaultShippingFee;
    }

    public static decimal Compute(TenantShippingSettings? settings, decimal merchandiseValue) =>
        settings == null ? 0m : Compute(settings.DefaultShippingFee, settings.FreeShippingThreshold, merchandiseValue);

    /// <summary>The fee of a parcel and the row that set it (null = the flat fee, or free because of the threshold).</summary>
    public static ShippingFeeResult Compute(TenantShippingSettings? settings, IEnumerable<ShippingFeeRule> rules, string? province, int weightGrams, decimal merchandiseValue)
    {
        var threshold = settings?.FreeShippingThreshold ?? 0m;
        var rule = rules.Where(r => r.IsActive).OrderBy(r => r.Priority).ThenBy(r => r.Name)
            .FirstOrDefault(r => r.Matches(province ?? string.Empty, weightGrams));

        var normally = rule?.Fee ?? Math.Max(0m, settings?.DefaultShippingFee ?? 0m);
        var free = normally > 0m && threshold > 0m && merchandiseValue >= threshold;
        return new ShippingFeeResult(free ? 0m : normally, free, rule);
    }
}

/// <param name="Fee">What the customer pays.</param>
/// <param name="FreeBecauseOfThreshold">The shop normally charges for this parcel but the goods reached the free-delivery threshold.</param>
/// <param name="Rule">The price-list row that applied; null when the flat fee did.</param>
public record ShippingFeeResult(decimal Fee, bool FreeBecauseOfThreshold, ShippingFeeRule? Rule);

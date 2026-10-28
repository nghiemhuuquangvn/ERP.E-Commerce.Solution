using ERP.SHP.Application.DTOs;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities.Settings;

namespace ERP.SHP.Application.Services;

/// <summary>
/// What the current shop charges the customer for delivery; the order service asks it, the storefront previews it. The first matching row of the shop's
/// price list (province x weight band) sets the fee, else the flat fee; the free-delivery threshold applies on top (ROADMAP P1-11b / P1-11c).
/// </summary>
public class ShippingFeeService
{
    private readonly IShippingUnitOfWork _uow;
    private readonly IProductWeightClient _weights;

    public ShippingFeeService(IShippingUnitOfWork uow, IProductWeightClient weights)
    {
        _uow = uow;
        _weights = weights;
    }

    /// <summary>The flat fee only: a caller that knows neither the province nor the products (P1-11b).</summary>
    public Task<ShippingFeeQuote> QuoteAsync(decimal merchandiseValue, CancellationToken ct) =>
        QuoteAsync(new ShippingFeeRequest { MerchandiseValue = merchandiseValue }, ct);

    public async Task<ShippingFeeQuote> QuoteAsync(ShippingFeeRequest request, CancellationToken ct)
    {
        if (request.MerchandiseValue < 0) throw new ArgumentOutOfRangeException(nameof(request), "The value of the goods cannot be negative.");
        if (request.Items.Any(i => i.Quantity <= 0)) throw new ArgumentOutOfRangeException(nameof(request), "A quantity must be above zero.");

        var settings = await _uow.Config.GetTenantSettingsAsync(ct);
        var rules = await _uow.Config.GetFeeRulesAsync(ct);

        var (grams, estimated) = await WeightAsync(request, settings?.DefaultItemWeightGrams ?? 500, ct);
        var result = ShippingFeePolicy.Compute(settings, rules, request.Province, grams, request.MerchandiseValue);
        return new ShippingFeeQuote(result.Fee, result.FreeBecauseOfThreshold, settings?.FreeShippingThreshold ?? 0m, result.Rule?.Name, grams, estimated);
    }

    /// <summary>The parcel's weight: each product's own weight, else the shop's default item weight. No products given = unknown (0, estimated).</summary>
    private async Task<(int Grams, bool Estimated)> WeightAsync(ShippingFeeRequest request, int defaultItemGrams, CancellationToken ct)
    {
        var items = request.Items;
        if (items.Count == 0) return request.WeightGrams is >= 0 and var given ? (given, false) : (0, true);

        var known = await _weights.GetWeightsAsync(items.Select(i => i.ProductId).Distinct().ToList(), ct);
        var estimated = false;
        long total = 0;
        foreach (var item in items)
        {
            if (known.TryGetValue(item.ProductId, out var grams)) total += (long)grams * item.Quantity;
            else { total += (long)defaultItemGrams * item.Quantity; estimated = true; }
        }
        return ((int)Math.Min(total, int.MaxValue), estimated);
    }
}

/// <param name="Fee">What the customer pays; 0 when delivery is free.</param>
/// <param name="IsFree">True when the shop normally charges but the goods reached the threshold.</param>
/// <param name="FreeShippingThreshold">The shop's threshold (0 = none), so the storefront can say how much is missing.</param>
/// <param name="RuleName">The price-list row that set the fee; null when the flat fee did.</param>
/// <param name="WeightGrams">The parcel weight the fee was worked out for (0 = unknown, no products were given).</param>
/// <param name="WeightEstimated">At least one product had no weight of its own (or none were given): the shop's default weight was used.</param>
public record ShippingFeeQuote(decimal Fee, bool IsFree, decimal FreeShippingThreshold, string? RuleName = null, int WeightGrams = 0, bool WeightEstimated = false);

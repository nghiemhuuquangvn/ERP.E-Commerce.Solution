using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities.Settings;

namespace ERP.SHP.Application.Services;

/// <summary>What the current shop charges the customer for delivery; the order service asks it, the storefront previews it.</summary>
public class ShippingFeeService
{
    private readonly IShippingUnitOfWork _uow;

    public ShippingFeeService(IShippingUnitOfWork uow) => _uow = uow;

    public async Task<ShippingFeeQuote> QuoteAsync(decimal merchandiseValue, CancellationToken ct)
    {
        if (merchandiseValue < 0) throw new ArgumentOutOfRangeException(nameof(merchandiseValue), "The value of the goods cannot be negative.");
        var settings = await _uow.Config.GetTenantSettingsAsync(ct);
        var fee = ShippingFeePolicy.Compute(settings, merchandiseValue);
        var threshold = settings?.FreeShippingThreshold ?? 0m;
        return new ShippingFeeQuote(fee, fee == 0m && settings is { DefaultShippingFee: > 0 }, threshold);
    }
}

/// <param name="Fee">What the customer pays; 0 when delivery is free.</param>
/// <param name="IsFree">True when the shop normally charges but the goods reached the threshold.</param>
/// <param name="FreeShippingThreshold">The shop's threshold (0 = none), so the storefront can say how much is missing.</param>
public record ShippingFeeQuote(decimal Fee, bool IsFree, decimal FreeShippingThreshold);

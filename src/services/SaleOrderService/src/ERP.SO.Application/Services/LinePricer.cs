using System.Globalization;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.SO.Application.Interfaces;

namespace ERP.SO.Application.Services
{
    /// <summary>One line as the client sent it: the price is optional and only a claim.</summary>
    public readonly record struct LineToPrice(Guid ProductId, string ProductName, decimal? ClientPrice);

    /// <summary>The price a line is booked at. <see cref="IsManual"/> = a price that differs from the system price, accepted because the caller may override.</summary>
    public readonly record struct PricedLine(decimal UnitPrice, decimal? SystemPrice, bool IsManual);

    /// <summary>
    /// The one rule that fixes the price of an order or quotation line (R113): the server asks ProductService; a price the client sends is accepted only when it
    /// equals the system price, or when the caller holds <c>sales_order:price_override</c> (a manual price). A customer whose cart is out of date is refused with the
    /// new price, and a product without a price cannot be ordered. Fails closed when ProductService cannot answer.
    /// </summary>
    public sealed class LinePricer
    {
        private readonly IProductPriceClient _prices;

        public LinePricer(IProductPriceClient prices) => _prices = prices;

        public async Task<IReadOnlyList<PricedLine>> PriceAsync(IReadOnlyList<LineToPrice> lines, bool allowOverride, CancellationToken ct = default)
        {
            var system = await _prices.GetPricesAsync(lines.Select(l => l.ProductId).Distinct().ToList(), ct)
                ?? throw new BusinessRuleException("Sales", "The prices cannot be checked right now. Please try again in a moment.");

            var priced = new List<PricedLine>(lines.Count);
            foreach (var line in lines)
            {
                var hasSystem = system.TryGetValue(line.ProductId, out var systemPrice) && systemPrice > 0;

                if (!hasSystem)
                {
                    if (line.ClientPrice is > 0 && allowOverride)
                    {
                        priced.Add(new PricedLine(line.ClientPrice.Value, null, true));
                        continue;
                    }

                    throw new BusinessRuleException("Sales", $"{line.ProductName} has no price set, so it cannot be ordered.");
                }

                if (line.ClientPrice is null || Money(line.ClientPrice.Value) == Money(systemPrice))
                {
                    priced.Add(new PricedLine(systemPrice, systemPrice, false));
                }
                else if (allowOverride)
                {
                    priced.Add(new PricedLine(line.ClientPrice.Value, systemPrice, true));
                }
                else
                {
                    throw new BusinessRuleException("Sales.PriceChanged",
                        $"The price of {line.ProductName} changed from {Format(line.ClientPrice.Value)} to {Format(systemPrice)}. Review the prices and try again.");
                }
            }

            return priced;
        }

        private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        public static string Format(decimal value) => Money(value).ToString("0.00", CultureInfo.InvariantCulture);
    }
}

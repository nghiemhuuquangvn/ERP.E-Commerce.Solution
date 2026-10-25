namespace ERP.ACC.Application.Posting
{
    /// <summary>The split of a document amount into net, tax and gross (ROADMAP P1-3). Gross is what the customer owes / the supplier is owed.</summary>
    public readonly record struct VatSplit(decimal Net, decimal RatePercent, decimal Tax, decimal Gross);

    /// <summary>The tax rate and the price convention a tenant uses when a document carries no tax of its own (TenantTaxSettings).</summary>
    public readonly record struct VatSettings(decimal RatePercent, bool PricesIncludeTax);

    public static class VatCalculator
    {
        /// <summary>
        /// Prices include tax: tax = round(gross × r ÷ (100 + r), 2), net = gross − tax. Prices exclude tax: net = amount, tax = round(net × r ÷ 100, 2), gross = net + tax.
        /// Rounded half away from zero; the rate is 0–100.
        /// </summary>
        public static VatSplit Split(decimal amount, decimal ratePercent, bool pricesIncludeTax)
        {
            if (ratePercent < 0m || ratePercent > 100m)
                throw new ArgumentOutOfRangeException(nameof(ratePercent), "The VAT rate must be between 0 and 100.");
            amount = Math.Round(amount, 2);
            if (ratePercent == 0m)
                return new VatSplit(amount, 0m, 0m, amount);
            if (pricesIncludeTax)
            {
                var tax = Math.Round(amount * ratePercent / (100m + ratePercent), 2, MidpointRounding.AwayFromZero);
                return new VatSplit(amount - tax, ratePercent, tax, amount);
            }
            var added = Math.Round(amount * ratePercent / 100m, 2, MidpointRounding.AwayFromZero);
            return new VatSplit(amount, ratePercent, added, amount + added);
        }

        /// <summary>The tax inside a gross amount at a rate (a return or a re-split keeps the gross).</summary>
        public static decimal TaxInGross(decimal gross, decimal ratePercent) => Split(gross, ratePercent, true).Tax;
    }
}

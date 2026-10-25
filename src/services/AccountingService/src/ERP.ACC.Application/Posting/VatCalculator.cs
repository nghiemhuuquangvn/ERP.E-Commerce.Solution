namespace ERP.ACC.Application.Posting
{
    /// <summary>The split of a document amount into net, tax and gross (ROADMAP P1-3). Gross is what the customer owes / the supplier is owed.</summary>
    public readonly record struct VatSplit(decimal Net, decimal RatePercent, decimal Tax, decimal Gross);

    /// <summary>The tax rate and the price convention a tenant uses when a document carries no tax of its own (TenantTaxSettings).</summary>
    public readonly record struct VatSettings(decimal RatePercent, bool PricesIncludeTax);

    /// <summary>A line as typed: category 0 taxable / 1 not subject to VAT, the rate (taxable only) and the line's amount (gross with prices including tax, otherwise net).</summary>
    public class VatLineInput
    {
        public int Category { get; set; }
        public decimal RatePercent { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }

    public record VatLineSplit(int Category, decimal RatePercent, decimal Net, decimal Tax, decimal Gross, string? Description);

    public record VatLinesSplit(IReadOnlyList<VatLineSplit> Lines, decimal Net, decimal Tax, decimal Gross)
    {
        /// <summary>The rate shown on the document: the one taxable rate, or 0 when the lines mix rates (or have none).</summary>
        public decimal SummaryRate
        {
            get
            {
                var rates = Lines.Where(l => l.Category == 0).Select(l => l.RatePercent).Distinct().ToList();
                return rates.Count == 1 ? rates[0] : 0m;
            }
        }
    }

    public static class VatCalculator
    {
        /// <summary>
        /// Splits several lines (ROADMAP P1-3b). Each taxable line is split like a whole document; a line not subject to VAT has net = gross and no tax. The document
        /// totals are the sums of the rounded lines, so net + tax = gross exactly.
        /// </summary>
        public static VatLinesSplit SplitLines(IReadOnlyCollection<VatLineInput> inputs, bool pricesIncludeTax)
        {
            if (inputs == null || inputs.Count == 0)
                throw new ArgumentException("At least one VAT line is needed.");
            var lines = new List<VatLineSplit>();
            foreach (var l in inputs)
            {
                if (l.Amount <= 0m)
                    throw new ArgumentException("A VAT line needs an amount above 0.");
                if (l.Category == 1)
                {
                    var amount = Math.Round(l.Amount, 2);
                    lines.Add(new VatLineSplit(1, 0m, amount, 0m, amount, l.Description));
                    continue;
                }
                if (l.Category != 0)
                    throw new ArgumentException("The VAT line category must be 0 (taxable) or 1 (not subject to VAT).");
                var s = Split(l.Amount, l.RatePercent, pricesIncludeTax);
                lines.Add(new VatLineSplit(0, s.RatePercent, s.Net, s.Tax, s.Gross, l.Description));
            }
            return new VatLinesSplit(lines, lines.Sum(x => x.Net), lines.Sum(x => x.Tax), lines.Sum(x => x.Gross));
        }

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

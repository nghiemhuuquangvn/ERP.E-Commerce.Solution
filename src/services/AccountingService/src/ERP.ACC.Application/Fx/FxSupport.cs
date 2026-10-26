using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Fx
{
    /// <summary>One unit of <see cref="From"/> in <see cref="To"/> on a day, and where it came from (ROADMAP P1-4).</summary>
    public record ExchangeRateQuote(string From, string To, decimal Rate, DateTime EffectiveDate, bool Inverted);

    /// <summary>Looks up the exchange rate entered in the master data (SharedMasterDataService); null when none is known or the service is down: the user then types the rate.</summary>
    public interface IExchangeRateProvider
    {
        Task<ExchangeRateQuote?> GetRateAsync(string from, string to, DateTime asOf, CancellationToken ct = default);
    }

    /// <summary>The conversion rules of foreign documents (ROADMAP P1-4): the books are kept in the base currency, a foreign document keeps its currency, rate and foreign gross.</summary>
    public static class FxSupport
    {
        public static decimal ToBase(decimal foreignAmount, decimal rate) => Math.Round(foreignAmount * rate, 2, MidpointRounding.AwayFromZero);

        /// <summary>Normalises a currency code and tells whether it is a foreign one. Refuses a rate that is missing or not positive for a foreign document.</summary>
        public static bool IsForeign(string? currency, decimal? rate, string baseCurrency)
        {
            if (string.IsNullOrWhiteSpace(currency) || string.Equals(currency.Trim(), baseCurrency, StringComparison.OrdinalIgnoreCase))
                return false;
            if (currency.Trim().Length != 3)
                throw new BusinessRuleException("Accounting", "The currency is a 3-letter code (for example USD).");
            if (rate is not > 0m)
                throw new BusinessRuleException("Accounting", $"An exchange rate (base currency per 1 {currency.Trim().ToUpperInvariant()}) is required for a document in {currency.Trim().ToUpperInvariant()}.");
            return true;
        }

        /// <summary>
        /// The gain (positive) or loss (negative) of settling a receivable: the cash received (foreign amount at the payment rate) against the carrying amount of what was settled.
        /// A payable is the opposite: paying less than the carrying amount is a gain, so call it with the sign flipped (<see cref="PayableDifference"/>).
        /// </summary>
        public static decimal ReceivableDifference(decimal cashInBase, decimal carryingInBase) => Math.Round(cashInBase - carryingInBase, 2);

        public static decimal PayableDifference(decimal cashInBase, decimal carryingInBase) => Math.Round(carryingInBase - cashInBase, 2);

        /// <summary>The base-currency total of a foreign document from its foreign total; the total and the rate are required and a foreign document takes no VAT lines yet.</summary>
        public static decimal ForeignToBase(decimal? foreignAmount, decimal rate, int lineCount)
        {
            if (lineCount > 0)
                throw new BusinessRuleException("Accounting", "VAT lines are not supported on a foreign-currency document yet: enter the total and one VAT rate.");
            if (foreignAmount is not > 0m)
                throw new BusinessRuleException("Accounting", "The amount in the foreign currency is required.");
            return ToBase(foreignAmount.Value, rate);
        }

        /// <summary>The gross in the foreign currency: the entered total as it is when prices include tax, else the total plus the VAT at the rate.</summary>
        public static decimal ForeignGross(decimal foreignAmount, decimal taxRatePercent, bool pricesIncludeTax) =>
            pricesIncludeTax ? foreignAmount : VatCalculator.Split(foreignAmount, taxRatePercent, false).Gross;

        public static string Normalise(string code) => code.Trim().ToUpperInvariant();
    }

    /// <summary>The suggested rate for a currency on a day (the admin fills the rate field with it); 404-like null when the master data has none.</summary>
    public class GetExchangeRateQuery : IRequest<ExchangeRateQuote?>
    {
        public string Currency { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
    }

    public class GetExchangeRateQueryHandler : IRequestHandler<GetExchangeRateQuery, ExchangeRateQuote?>
    {
        private readonly IExchangeRateProvider _provider;
        private readonly IVatPolicy _policy;

        public GetExchangeRateQueryHandler(IExchangeRateProvider provider, IVatPolicy policy)
        {
            _provider = provider;
            _policy = policy;
        }

        public async Task<ExchangeRateQuote?> Handle(GetExchangeRateQuery request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3)
                throw new BusinessRuleException("Accounting", "The currency is a 3-letter code (for example USD).");
            var baseCurrency = (await _policy.GetAsync(ct)).BaseCurrency;
            return await _provider.GetRateAsync(FxSupport.Normalise(request.Currency), baseCurrency, (request.Date ?? DateTime.UtcNow).Date, ct);
        }
    }
}

using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class ExchangeRate : AggregateRoot
    {
        public Guid FromCurrencyId { get; set; }
        public Guid ToCurrencyId { get; set; }
        public decimal Rate { get; set; }
        public DateTime EffectiveDate { get; set; }

        public static ExchangeRate Create(
            Guid fromCurrencyId,
            Guid toCurrencyId,
            decimal rate,
            DateTime effectiveDate)
        {
            return new ExchangeRate
            {
                Id = Guid.NewGuid(),
                FromCurrencyId = fromCurrencyId,
                ToCurrencyId = toCurrencyId,
                Rate = rate,
                EffectiveDate = effectiveDate,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void Update(decimal rate, DateTime effectiveDate)
        {
            Rate = rate;
            EffectiveDate = effectiveDate;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

using ERP.MST.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class Currency : AggregateRoot
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public int DecimalPlaces { get; set; } = 2;
        public bool IsActive { get; set; } = true;

        public static Currency Create(
            string code,
            string name,
            string symbol,
            int decimalPlaces = 2)
        {
            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                Symbol = symbol,
                DecimalPlaces = decimalPlaces,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            currency.AddDomainEvent(new CurrencyCreatedEvent(currency.Id, currency.Code, currency.Name));

            return currency;
        }

        public void Update(string name, string symbol, int decimalPlaces)
        {
            Name = name;
            Symbol = symbol;
            DecimalPlaces = decimalPlaces;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Activate()
        {
            IsActive = true;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

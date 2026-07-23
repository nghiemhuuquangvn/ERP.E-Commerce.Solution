using ERP.ACC.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    public class TaxRate : AggregateRoot
    {
        public string Name { get; private set; } = string.Empty;
        public string Code { get; private set; } = string.Empty;
        public decimal Rate { get; private set; }
        public bool IsActive { get; private set; }
        public new string? Description { get; private set; }

        private TaxRate() { }

        public static TaxRate Create(string name, string code, decimal rate, string? description = null)
        {
            var taxRate = new TaxRate
            {
                Id = Guid.NewGuid(),
                Name = name,
                Code = code,
                Rate = rate,
                IsActive = true,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };
            taxRate.AddDomainEvent(new TaxRateCreatedEvent(taxRate.Id, name, code, rate));
            return taxRate;
        }

        public void Update(string name, string code, decimal rate, string? description = null)
        {
            Name = name;
            Code = code;
            Rate = rate;
            Description = description;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Deactivate()
        {
            if (!IsActive) throw new InvalidOperationException($"Tax rate {Code} is already inactive.");
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

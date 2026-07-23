using ERP.MST.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class Company : AggregateRoot
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? TaxCode { get; set; }
        public string? LegalAddress { get; set; }
        public string? CurrencyCode { get; set; }
        public bool IsActive { get; set; } = true;

        public static Company Create(
            string name,
            string code,
            string? taxCode = null,
            string? legalAddress = null,
            string? currencyCode = null)
        {
            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = name,
                Code = code,
                TaxCode = taxCode,
                LegalAddress = legalAddress,
                CurrencyCode = currencyCode,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            company.AddDomainEvent(new CompanyCreatedEvent(company.Id, company.Name, company.Code));

            return company;
        }

        public void Update(string name, string code, string? taxCode, string? legalAddress, string? currencyCode)
        {
            Name = name;
            Code = code;
            TaxCode = taxCode;
            LegalAddress = legalAddress;
            CurrencyCode = currencyCode;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new CompanyUpdatedEvent(Id, Name, Code));
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

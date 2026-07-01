using ERP.MST.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class Country : AggregateRoot
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? PhoneCode { get; set; }

        public static Country Create(string code, string name, string? phoneCode = null)
        {
            var country = new Country
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                PhoneCode = phoneCode,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            country.AddDomainEvent(new CountryCreatedEvent(country.Id, country.Code, country.Name));

            return country;
        }
    }
}

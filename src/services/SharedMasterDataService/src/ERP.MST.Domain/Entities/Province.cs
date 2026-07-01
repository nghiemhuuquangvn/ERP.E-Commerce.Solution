using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class Province : AggregateRoot
    {
        public Guid CountryId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public static Province Create(Guid countryId, string code, string name)
        {
            var province = new Province
            {
                Id = Guid.NewGuid(),
                CountryId = countryId,
                Code = code,
                Name = name,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return province;
        }
    }
}

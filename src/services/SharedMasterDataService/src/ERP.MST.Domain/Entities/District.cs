using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class District : AggregateRoot
    {
        public Guid ProvinceId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public static District Create(Guid provinceId, string code, string name)
        {
            var district = new District
            {
                Id = Guid.NewGuid(),
                ProvinceId = provinceId,
                Code = code,
                Name = name,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return district;
        }
    }
}

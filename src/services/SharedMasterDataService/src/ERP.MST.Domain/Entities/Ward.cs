using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class Ward : AggregateRoot
    {
        public Guid DistrictId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public static Ward Create(Guid districtId, string code, string name)
        {
            var ward = new Ward
            {
                Id = Guid.NewGuid(),
                DistrictId = districtId,
                Code = code,
                Name = name,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return ward;
        }
    }
}

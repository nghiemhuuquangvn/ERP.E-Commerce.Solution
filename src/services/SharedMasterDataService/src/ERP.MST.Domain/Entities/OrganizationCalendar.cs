using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class OrganizationCalendar : AggregateRoot
    {
        public Guid CompanyId { get; set; }
        public int Year { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }

        public static OrganizationCalendar Create(
            Guid companyId,
            int year,
            string name,
            bool isDefault = false)
        {
            return new OrganizationCalendar
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Year = year,
                Name = name,
                IsDefault = isDefault,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void Update(string name, bool isDefault)
        {
            Name = name;
            IsDefault = isDefault;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

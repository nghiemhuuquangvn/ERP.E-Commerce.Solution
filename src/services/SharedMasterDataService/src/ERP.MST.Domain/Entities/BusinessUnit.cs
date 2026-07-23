using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class BusinessUnit : AggregateRoot
    {
        public Guid CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        public static BusinessUnit Create(
            Guid companyId,
            string name,
            string code)
        {
            return new BusinessUnit
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Name = name,
                Code = code,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void Update(string name, string code)
        {
            Name = name;
            Code = code;
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

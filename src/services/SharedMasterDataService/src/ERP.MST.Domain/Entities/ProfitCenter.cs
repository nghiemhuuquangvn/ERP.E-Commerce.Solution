using ERP.MST.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class ProfitCenter : AggregateRoot
    {
        public Guid CompanyId { get; set; }
        public Guid? BusinessUnitId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        public static ProfitCenter Create(
            Guid companyId,
            string name,
            string code,
            Guid? businessUnitId = null)
        {
            var profitCenter = new ProfitCenter
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                BusinessUnitId = businessUnitId,
                Name = name,
                Code = code,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            profitCenter.AddDomainEvent(new ProfitCenterCreatedEvent(profitCenter.Id, profitCenter.CompanyId, profitCenter.Name, profitCenter.Code));

            return profitCenter;
        }

        public void Update(string name, string code, Guid? businessUnitId)
        {
            Name = name;
            Code = code;
            BusinessUnitId = businessUnitId;
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

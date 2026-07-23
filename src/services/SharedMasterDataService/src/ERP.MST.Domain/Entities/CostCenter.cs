using ERP.MST.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class CostCenter : AggregateRoot
    {
        public Guid CompanyId { get; set; }
        public Guid? DepartmentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal BudgetAmount { get; set; }
        public bool IsActive { get; set; } = true;

        public static CostCenter Create(
            Guid companyId,
            string name,
            string code,
            decimal budgetAmount,
            Guid? departmentId = null)
        {
            var costCenter = new CostCenter
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                DepartmentId = departmentId,
                Name = name,
                Code = code,
                BudgetAmount = budgetAmount,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            costCenter.AddDomainEvent(new CostCenterCreatedEvent(costCenter.Id, costCenter.CompanyId, costCenter.Name, costCenter.Code));

            return costCenter;
        }

        public void Update(string name, string code, decimal budgetAmount, Guid? departmentId)
        {
            Name = name;
            Code = code;
            BudgetAmount = budgetAmount;
            DepartmentId = departmentId;
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

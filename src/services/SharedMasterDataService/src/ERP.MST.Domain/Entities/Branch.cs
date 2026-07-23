using ERP.MST.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class Branch : AggregateRoot
    {
        public Guid CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Address { get; set; }
        public Guid? WardId { get; set; }
        public Guid? ManagerEmployeeId { get; set; }
        public bool IsActive { get; set; } = true;

        public static Branch Create(
            Guid companyId,
            string name,
            string code,
            string? address = null,
            Guid? wardId = null,
            Guid? managerEmployeeId = null)
        {
            var branch = new Branch
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Name = name,
                Code = code,
                Address = address,
                WardId = wardId,
                ManagerEmployeeId = managerEmployeeId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            branch.AddDomainEvent(new BranchCreatedEvent(branch.Id, branch.CompanyId, branch.Name, branch.Code));

            return branch;
        }

        public void Update(string name, string code, string? address, Guid? wardId, Guid? managerEmployeeId)
        {
            Name = name;
            Code = code;
            Address = address;
            WardId = wardId;
            ManagerEmployeeId = managerEmployeeId;
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

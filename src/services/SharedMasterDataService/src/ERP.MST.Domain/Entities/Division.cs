using ERP.MST.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class Division : AggregateRoot
    {
        public Guid BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public Guid? ParentDivisionId { get; set; }
        public bool IsActive { get; set; } = true;

        public static Division Create(
            Guid branchId,
            string name,
            string code,
            Guid? parentDivisionId = null)
        {
            var division = new Division
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                Name = name,
                Code = code,
                ParentDivisionId = parentDivisionId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            division.AddDomainEvent(new DivisionCreatedEvent(division.Id, division.BranchId, division.Name, division.Code));

            return division;
        }

        public void Update(string name, string code, Guid? parentDivisionId)
        {
            Name = name;
            Code = code;
            ParentDivisionId = parentDivisionId;
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

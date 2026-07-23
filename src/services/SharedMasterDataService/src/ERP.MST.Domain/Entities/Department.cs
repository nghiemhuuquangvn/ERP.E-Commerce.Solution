using ERP.MST.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class Department : AggregateRoot
    {
        public string DepartmentName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public Guid? ManagerId { get; set; }
        public Guid? ParentDepartmentId { get; set; }
        public Guid? DivisionId { get; set; }

        public static Department Create(
            string departmentName,
            string code,
            Guid? managerId = null,
            Guid? parentDepartmentId = null,
            Guid? divisionId = null)
        {
            var department = new Department
            {
                Id = Guid.NewGuid(),
                DepartmentName = departmentName,
                Code = code,
                ManagerId = managerId,
                ParentDepartmentId = parentDepartmentId,
                DivisionId = divisionId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            department.AddDomainEvent(new DepartmentCreatedEvent(department.Id, department.DepartmentName, department.Code));

            return department;
        }

        public void Update(string departmentName, string code, Guid? managerId)
        {
            DepartmentName = departmentName;
            Code = code;
            ManagerId = managerId;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new DepartmentUpdatedEvent(Id, DepartmentName, Code));
        }

        public void SetDivision(Guid? divisionId)
        {
            DivisionId = divisionId;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

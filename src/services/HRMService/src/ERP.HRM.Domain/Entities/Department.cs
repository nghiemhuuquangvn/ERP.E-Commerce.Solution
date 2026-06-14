using ERP.Shared.Core.Domain;

namespace ERP.HRM.Domain.Entities
{
    public class Department : AggregateRoot
    {
        public string DepartmentName { get; set; } = string.Empty;
        public Guid? ManagerId { get; set; }
        public Guid? ParentDepartmentId { get; set; }

        public static Department Create(string departmentName, Guid? managerId = null, Guid? parentDepartmentId = null)
        {
            var department = new Department
            {
                Id = Guid.NewGuid(),
                DepartmentName = departmentName,
                ManagerId = managerId,
                ParentDepartmentId = parentDepartmentId,
                CreatedAt = DateTime.UtcNow
            };

            return department;
        }
    }
}

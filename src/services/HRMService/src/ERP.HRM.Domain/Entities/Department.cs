using ERP.Shared.Core.Domain;

namespace ERP.HRM.Domain.Entities
{
    public class Department : AggregateRoot
    {
        public string DepartmentName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public Guid? ManagerId { get; set; }
        public Guid? ParentDepartmentId { get; set; }

        public static Department Create(
            string departmentName,
            string code,
            Guid? managerId = null,
            Guid? parentDepartmentId = null)
        {
            var department = new Department
            {
                Id = Guid.NewGuid(),
                DepartmentName = departmentName,
                Code = code,
                ManagerId = managerId,
                ParentDepartmentId = parentDepartmentId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return department;
        }

        public void Update(string departmentName, string code, Guid? managerId)
        {
            DepartmentName = departmentName;
            Code = code;
            ManagerId = managerId;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

using ERP.Shared.Core.Domain;

namespace ERP.HRM.Domain.Entities
{
    public class Employee : AggregateRoot
    {
        public string EmployeeNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public DateTime DateOfHire { get; set; }
        public Guid DepartmentId { get; set; }
        public int EmploymentStatus { get; set; }
        public int EmploymentType { get; set; }

        public static Employee Create(string employeeNumber, string firstName, string lastName, Guid departmentId, DateTime dateOfHire)
        {
            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                EmployeeNumber = employeeNumber,
                FirstName = firstName,
                LastName = lastName,
                DepartmentId = departmentId,
                DateOfHire = dateOfHire,
                EmploymentStatus = 0,
                EmploymentType = 0,
                CreatedAt = DateTime.UtcNow
            };

            return employee;
        }
    }
}

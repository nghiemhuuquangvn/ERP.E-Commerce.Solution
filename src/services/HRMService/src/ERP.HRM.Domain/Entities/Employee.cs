using ERP.HRM.Domain.Events;
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

        public string FullName => $"{FirstName} {LastName}";

        public static Employee Create(
            string employeeNumber,
            string firstName,
            string lastName,
            Guid departmentId,
            DateTime dateOfHire,
            int employmentType = 0)
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
                EmploymentType = employmentType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return employee;
        }

        public void RaiseHiredEvent()
        {
            AddDomainEvent(new EmployeeHiredEvent(Id, EmployeeNumber, FullName, Email, DepartmentId));
        }

        public void Terminate(string reason)
        {
            if (EmploymentStatus == (int)Enums.EmploymentStatus.Terminated)
                throw new InvalidOperationException("Employee already terminated.");

            EmploymentStatus = (int)Enums.EmploymentStatus.Terminated;
            UpdatedAt = DateTime.UtcNow;
            AddDomainEvent(new EmployeeTerminatedEvent(Id, reason));
        }

        public void SetOnLeave()
        {
            EmploymentStatus = (int)Enums.EmploymentStatus.OnLeave;
            UpdatedAt = DateTime.UtcNow;
        }

        public void ReturnFromLeave()
        {
            EmploymentStatus = (int)Enums.EmploymentStatus.Active;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Transfer(Guid newDepartmentId)
        {
            var oldDepartmentId = DepartmentId;
            DepartmentId = newDepartmentId;
            UpdatedAt = DateTime.UtcNow;
            AddDomainEvent(new EmployeeTransferredEvent(Id, oldDepartmentId, newDepartmentId));
        }

        public void UpdateProfile(string? email, string? phone)
        {
            Email = email;
            Phone = phone;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

using ERP.Shared.Core.Domain;

namespace ERP.HRM.Domain.Entities
{
    /// <summary>
    /// Read-only local projection of an Employee owned by SharedMasterDataService.
    /// Populated by consuming EmployeeHiredEvent/EmployeeTerminatedEvent/
    /// EmployeeTransferredEvent from "mst-topic" — never written to for any other
    /// reason. Exists so LeaveRequest/PaySlip handlers can check employee status
    /// and enumerate active employees locally, without a synchronous cross-service
    /// call (this codebase has none). Not an AggregateRoot: it never raises its
    /// own domain events.
    /// </summary>
    public class EmployeeSnapshot : BaseEntity
    {
        public string FullName { get; set; } = string.Empty;
        public int EmploymentStatus { get; set; }
        public Guid DepartmentId { get; set; }

        public static EmployeeSnapshot Create(Guid employeeId, string fullName, int employmentStatus, Guid departmentId)
        {
            return new EmployeeSnapshot
            {
                Id = employeeId,
                FullName = fullName,
                EmploymentStatus = employmentStatus,
                DepartmentId = departmentId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}

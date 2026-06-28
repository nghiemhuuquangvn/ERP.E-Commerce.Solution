using ERP.HRM.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.HRM.Domain.Entities
{
    public class LeaveRequest : AggregateRoot
    {
        public string LeaveRequestNumber { get; set; } = string.Empty;
        public Guid EmployeeId { get; set; }
        public int LeaveTypeId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int DaysRequested { get; set; }
        public int LeaveStatus { get; set; }
        public string? Reason { get; set; }

        public static LeaveRequest Create(
            string leaveRequestNumber,
            Guid employeeId,
            int leaveTypeId,
            DateTime startDate,
            DateTime endDate,
            int daysRequested,
            string? reason = null)
        {
            var leaveRequest = new LeaveRequest
            {
                Id = Guid.NewGuid(),
                LeaveRequestNumber = leaveRequestNumber,
                EmployeeId = employeeId,
                LeaveTypeId = leaveTypeId,
                StartDate = startDate,
                EndDate = endDate,
                DaysRequested = daysRequested,
                LeaveStatus = 0,
                Reason = reason,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return leaveRequest;
        }

        public void Approve()
        {
            if (LeaveStatus != (int)Enums.LeaveStatus.Pending)
                throw new InvalidOperationException("Can only approve pending requests.");

            LeaveStatus = (int)Enums.LeaveStatus.Approved;
            UpdatedAt = DateTime.UtcNow;
            AddDomainEvent(new LeaveApprovedEvent(Id, EmployeeId, StartDate, EndDate));
        }

        public void Reject(string reason)
        {
            if (LeaveStatus != (int)Enums.LeaveStatus.Pending)
                throw new InvalidOperationException("Can only reject pending requests.");

            LeaveStatus = (int)Enums.LeaveStatus.Rejected;
            UpdatedAt = DateTime.UtcNow;
            AddDomainEvent(new LeaveRejectedEvent(Id, EmployeeId, reason));
        }

        public void Cancel()
        {
            if (LeaveStatus == (int)Enums.LeaveStatus.Approved || LeaveStatus == (int)Enums.LeaveStatus.Rejected)
                throw new InvalidOperationException("Cannot cancel an approved or rejected request.");

            LeaveStatus = (int)Enums.LeaveStatus.Cancelled;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

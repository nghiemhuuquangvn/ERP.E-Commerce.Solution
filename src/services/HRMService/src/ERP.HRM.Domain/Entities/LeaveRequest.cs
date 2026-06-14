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

        public static LeaveRequest Create(string leaveRequestNumber, Guid employeeId, int leaveTypeId, DateTime startDate, DateTime endDate, int daysRequested)
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
                CreatedAt = DateTime.UtcNow
            };

            return leaveRequest;
        }
    }
}

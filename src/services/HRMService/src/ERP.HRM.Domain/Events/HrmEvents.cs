using ERP.Shared.Core.Domain;

namespace ERP.HRM.Domain.Events;

// Employee/Department lifecycle events moved to SharedMasterDataService
// (ERP.MST.Domain.Events.MasterDataEvents) — HRM no longer owns those aggregates.

public record LeaveApprovedEvent(
    Guid LeaveRequestId,
    Guid EmployeeId,
    DateTime StartDate,
    DateTime EndDate) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record LeaveRejectedEvent(
    Guid LeaveRequestId,
    Guid EmployeeId,
    string Reason) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record PaySlipGeneratedEvent(
    Guid PaySlipId,
    Guid EmployeeId,
    decimal NetPay,
    string PayPeriod) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

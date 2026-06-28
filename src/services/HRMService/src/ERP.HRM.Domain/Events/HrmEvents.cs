using ERP.Shared.Core.Domain;

namespace ERP.HRM.Domain.Events;

public record EmployeeHiredEvent(
    Guid EmployeeId,
    string EmployeeNumber,
    string FullName,
    string? Email,
    Guid DepartmentId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record EmployeeTerminatedEvent(
    Guid EmployeeId,
    string Reason) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record EmployeeTransferredEvent(
    Guid EmployeeId,
    Guid OldDepartmentId,
    Guid NewDepartmentId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

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

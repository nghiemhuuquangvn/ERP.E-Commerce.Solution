using ERP.Shared.Core.Domain;

namespace ERP.HRM.Domain.Events;

// Employee/Department lifecycle events moved to SharedMasterDataService
// (ERP.MST.Domain.Events.MasterDataEvents) — HRM no longer owns those aggregates.

public record AttendanceCheckedInEvent(
    Guid AttendanceRecordId,
    Guid EmployeeId,
    DateTime Date,
    DateTime CheckInTime) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record AttendanceCheckedOutEvent(
    Guid AttendanceRecordId,
    Guid EmployeeId,
    DateTime CheckOutTime,
    decimal TotalHours,
    decimal OvertimeHours) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record LeaveApprovedEvent(
    Guid LeaveRequestId,
    Guid EmployeeId,
    DateTime StartDate,
    DateTime EndDate) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record LeaveRejectedEvent(
    Guid LeaveRequestId,
    Guid EmployeeId,
    string Reason) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record PaySlipGeneratedEvent(
    Guid PaySlipId,
    Guid EmployeeId,
    decimal NetPay,
    string PayPeriod) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

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

/// <summary>
/// A pay slip was generated. Reporting matches this type by the substring "PaySlipGenerated" and reads PaySlipId / EmployeeId / PayPeriod:
/// keep those names, and never give another event a name that contains that substring. <c>GrossSalary</c>, <c>OvertimePay</c> and
/// <c>Deductions</c> (ROADMAP P1-1d) are 0 on events published before the ledger posting existed; <c>NetPay = GrossSalary + OvertimePay - Deductions</c>.
/// </summary>
public record PaySlipGeneratedEvent(
    Guid PaySlipId,
    Guid EmployeeId,
    decimal NetPay,
    string PayPeriod,
    decimal GrossSalary = 0m,
    decimal OvertimePay = 0m,
    decimal Deductions = 0m,
    DateTime? PeriodEnd = null) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>The net pay of a pay slip was paid out (ROADMAP P1-1d): the ledger clears the salaries payable against the bank.</summary>
public record PaySlipPaidEvent(
    Guid PaySlipId,
    Guid EmployeeId,
    decimal NetPay,
    string PayPeriod,
    DateTime PaidOn,
    string? PaymentMethod) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>An unpaid pay slip was cancelled (ROADMAP P1-1d): the ledger reverses its entry; the period can be generated again.</summary>
public record PaySlipVoidedEvent(
    Guid PaySlipId,
    Guid EmployeeId,
    string PayPeriod,
    string Reason) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

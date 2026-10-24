using ERP.HRM.Application.DTOs;
using MediatR;

namespace ERP.HRM.Application.Commands;

// Department/Employee commands moved to SharedMasterDataService.
// HRM keeps a local, read-only EmployeeSnapshot projection instead — see
// EmployeeMasterDataEventHandlers and the commands below that keep it in sync.

// ─── Employee Snapshot Sync Commands (driven by mst-topic consumer) ───────

public record UpsertEmployeeSnapshotCommand(
    Guid EmployeeId,
    string FullName,
    int EmploymentStatus,
    Guid DepartmentId,
    string? EmployeeNumber = null,
    string? Email = null) : IRequest<Unit>;

public record MarkEmployeeSnapshotTerminatedCommand(
    Guid EmployeeId) : IRequest<Unit>;

public record UpdateEmployeeSnapshotDepartmentCommand(
    Guid EmployeeId,
    Guid NewDepartmentId) : IRequest<Unit>;

// ─── Leave Request Commands ────────────────────────────────────────────────

public record SubmitLeaveRequestCommand(
    Guid EmployeeId,
    int LeaveType,
    DateTime StartDate,
    DateTime EndDate,
    string? Reason) : IRequest<LeaveRequestDto>;

public record ApproveLeaveCommand(
    Guid LeaveRequestId) : IRequest<LeaveRequestDto>;

public record RejectLeaveCommand(
    Guid LeaveRequestId,
    string Reason) : IRequest<LeaveRequestDto>;

public record CancelLeaveCommand(
    Guid LeaveRequestId) : IRequest<LeaveRequestDto>;

// ─── Payroll Commands ──────────────────────────────────────────────────────

public record ProcessPayrollCommand(
    int Year,
    int Month) : IRequest<List<PaySlipDto>>;

public record GeneratePaySlipCommand(
    Guid EmployeeId,
    int Year,
    int Month,
    decimal GrossPay,
    decimal Deductions,
    decimal OvertimeHours = 0m,
    decimal OvertimePay = 0m) : IRequest<PaySlipDto>;

public record PayPaySlipCommand(
    Guid PaySlipId,
    DateTime? PaidOn = null,
    string? PaymentMethod = null) : IRequest<PaySlipDto>;

public record CancelPaySlipCommand(
    Guid PaySlipId,
    string Reason) : IRequest<PaySlipDto>;

// ─── Attendance Commands ───────────────────────────────────────────────────

public record CheckInCommand(
    Guid EmployeeId,
    DateTime CheckInTime,
    Guid? WorkShiftId = null,
    string? Notes = null) : IRequest<AttendanceDto>;

public record CheckOutCommand(
    Guid AttendanceRecordId,
    DateTime CheckOutTime,
    string? Notes = null) : IRequest<AttendanceDto>;

public record MarkAbsentCommand(
    Guid EmployeeId,
    DateTime Date,
    string? Notes = null) : IRequest<AttendanceDto>;

// ─── Work Shift Commands ───────────────────────────────────────────────────

public record CreateWorkShiftCommand(
    string Name,
    string StartTime,
    string EndTime) : IRequest<WorkShiftDto>;

public record UpdateWorkShiftCommand(
    Guid ShiftId,
    string Name,
    string StartTime,
    string EndTime) : IRequest<WorkShiftDto>;

public record DeactivateWorkShiftCommand(Guid ShiftId) : IRequest<WorkShiftDto>;

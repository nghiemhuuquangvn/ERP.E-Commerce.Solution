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
    Guid DepartmentId) : IRequest<Unit>;

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
    decimal Deductions) : IRequest<PaySlipDto>;

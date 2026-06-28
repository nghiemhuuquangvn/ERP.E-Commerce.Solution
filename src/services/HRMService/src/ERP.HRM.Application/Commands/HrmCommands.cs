using ERP.HRM.Application.DTOs;
using MediatR;

namespace ERP.HRM.Application.Commands;

// ─── Department Commands ───────────────────────────────────────────────────

public record CreateDepartmentCommand(
    string Name,
    string Code,
    Guid? ManagerId,
    Guid? ParentDepartmentId = null) : IRequest<DepartmentDto>;

public record UpdateDepartmentCommand(
    Guid DepartmentId,
    string Name,
    string Code,
    Guid? ManagerId) : IRequest<DepartmentDto>;

// ─── Employee Commands ─────────────────────────────────────────────────────

public record HireEmployeeCommand(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    Guid DepartmentId,
    DateTime DateOfHire,
    int EmploymentType) : IRequest<EmployeeDto>;

public record TerminateEmployeeCommand(
    Guid EmployeeId,
    string Reason) : IRequest<EmployeeDto>;

public record UpdateEmployeeProfileCommand(
    Guid EmployeeId,
    string? Email,
    string? Phone) : IRequest<EmployeeDto>;

public record TransferEmployeeCommand(
    Guid EmployeeId,
    Guid NewDepartmentId) : IRequest<EmployeeDto>;

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

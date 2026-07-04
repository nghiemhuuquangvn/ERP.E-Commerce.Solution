using ERP.HRM.Application.DTOs;
using MediatR;

namespace ERP.HRM.Application.Queries;

// Department/Employee queries moved to SharedMasterDataService.

// ─── Employee Snapshot Queries ─────────────────────────────────────────────

public record GetEmployeeSnapshotQuery(Guid EmployeeId) : IRequest<EmployeeSnapshotDto?>;

public record ListEmployeeSnapshotsQuery(
    string? Search = null,
    Guid? DepartmentId = null,
    int? EmploymentStatus = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<EmployeeSnapshotDto>>;

// ─── Leave Request Queries ─────────────────────────────────────────────────

public record GetLeaveRequestQuery(Guid LeaveRequestId) : IRequest<LeaveRequestDto?>;

public record ListLeaveRequestsQuery(
    Guid? EmployeeId = null,
    int? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<LeaveRequestDto>>;

// ─── PaySlip Queries ───────────────────────────────────────────────────────

public record GetPaySlipQuery(Guid PaySlipId) : IRequest<PaySlipDto?>;

public record ListPaySlipsQuery(
    Guid? EmployeeId = null,
    int? Year = null,
    int? Month = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<PaySlipDto>>;

// ─── Attendance Queries ────────────────────────────────────────────────────

public record GetAttendanceQuery(Guid AttendanceRecordId) : IRequest<AttendanceDto?>;

public record GetAttendanceByDateQuery(Guid EmployeeId, DateTime Date) : IRequest<AttendanceDto?>;

public record ListAttendanceQuery(
    Guid? EmployeeId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<AttendanceDto>>;

public record GetAttendanceSummaryQuery(
    Guid EmployeeId,
    int Year,
    int Month) : IRequest<AttendanceSummaryDto>;

// ─── Work Shift Queries ────────────────────────────────────────────────────

public record GetWorkShiftQuery(Guid ShiftId) : IRequest<WorkShiftDto?>;

public record ListWorkShiftsQuery(bool? ActiveOnly = null) : IRequest<IEnumerable<WorkShiftDto>>;

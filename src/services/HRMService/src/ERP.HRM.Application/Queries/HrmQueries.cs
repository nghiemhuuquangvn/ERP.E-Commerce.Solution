using ERP.HRM.Application.DTOs;
using MediatR;

namespace ERP.HRM.Application.Queries;

// ─── Department Queries ────────────────────────────────────────────────────

public record GetDepartmentQuery(Guid DepartmentId) : IRequest<DepartmentDto?>;

public record ListDepartmentsQuery(
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<DepartmentDto>>;

// ─── Employee Queries ──────────────────────────────────────────────────────

public record GetEmployeeQuery(Guid EmployeeId) : IRequest<EmployeeDto?>;

public record ListEmployeesQuery(
    Guid? DepartmentId = null,
    int? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<EmployeeDto>>;

public record GetHeadcountReportQuery() : IRequest<List<DepartmentHeadcountDto>>;

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

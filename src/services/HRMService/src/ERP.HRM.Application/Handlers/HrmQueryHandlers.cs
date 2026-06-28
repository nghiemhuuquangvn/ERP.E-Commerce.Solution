using ERP.HRM.Application.DTOs;
using ERP.HRM.Application.Handlers;
using ERP.HRM.Application.Interfaces;
using ERP.HRM.Application.Queries;
using ERP.HRM.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.HRM.Application.Handlers;

// ─── Department Query Handlers ─────────────────────────────────────────────

public class GetDepartmentQueryHandler : IRequestHandler<GetDepartmentQuery, DepartmentDto?>
{
    private readonly IDepartmentRepository _departmentRepository;

    public GetDepartmentQueryHandler(IDepartmentRepository departmentRepository)
    {
        _departmentRepository = departmentRepository;
    }

    public async Task<DepartmentDto?> Handle(GetDepartmentQuery request, CancellationToken cancellationToken)
    {
        var department = await _departmentRepository.GetByIdAsync(request.DepartmentId, cancellationToken);
        return department is null ? null : CreateDepartmentCommandHandler.MapToDto(department);
    }
}

public class ListDepartmentsQueryHandler : IRequestHandler<ListDepartmentsQuery, PagedResult<DepartmentDto>>
{
    private readonly IDepartmentRepository _departmentRepository;

    public ListDepartmentsQueryHandler(IDepartmentRepository departmentRepository)
    {
        _departmentRepository = departmentRepository;
    }

    public async Task<PagedResult<DepartmentDto>> Handle(ListDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _departmentRepository.ListAsync(request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateDepartmentCommandHandler.MapToDto).ToList();
        return new PagedResult<DepartmentDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ─── Employee Query Handlers ───────────────────────────────────────────────

public class GetEmployeeQueryHandler : IRequestHandler<GetEmployeeQuery, EmployeeDto?>
{
    private readonly IEmployeeRepository _employeeRepository;

    public GetEmployeeQueryHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<EmployeeDto?> Handle(GetEmployeeQuery request, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken);
        return employee is null ? null : HireEmployeeCommandHandler.MapToDto(employee);
    }
}

public class ListEmployeesQueryHandler : IRequestHandler<ListEmployeesQuery, PagedResult<EmployeeDto>>
{
    private readonly IEmployeeRepository _employeeRepository;

    public ListEmployeesQueryHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<PagedResult<EmployeeDto>> Handle(ListEmployeesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _employeeRepository.ListAsync(
            request.DepartmentId,
            request.Status,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(HireEmployeeCommandHandler.MapToDto).ToList();
        return new PagedResult<EmployeeDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

public class GetHeadcountReportQueryHandler : IRequestHandler<GetHeadcountReportQuery, List<DepartmentHeadcountDto>>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IEmployeeRepository _employeeRepository;

    public GetHeadcountReportQueryHandler(
        IDepartmentRepository departmentRepository,
        IEmployeeRepository employeeRepository)
    {
        _departmentRepository = departmentRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<List<DepartmentHeadcountDto>> Handle(GetHeadcountReportQuery request, CancellationToken cancellationToken)
    {
        var departments = await _departmentRepository.GetAllAsync(cancellationToken);
        var (allEmployees, _) = await _employeeRepository.ListAsync(null, null, 1, int.MaxValue, cancellationToken);

        var result = departments.Select(dept =>
        {
            var deptEmployees = allEmployees.Where(e => e.DepartmentId == dept.Id).ToList();
            return new DepartmentHeadcountDto(
                dept.Id,
                dept.DepartmentName,
                dept.Code,
                deptEmployees.Count(e => e.EmploymentStatus == (int)EmploymentStatus.Active),
                deptEmployees.Count(e => e.EmploymentStatus == (int)EmploymentStatus.OnLeave),
                deptEmployees.Count(e => e.EmploymentStatus == (int)EmploymentStatus.Terminated),
                deptEmployees.Count);
        }).ToList();

        return result;
    }
}

// ─── Leave Request Query Handlers ──────────────────────────────────────────

public class GetLeaveRequestQueryHandler : IRequestHandler<GetLeaveRequestQuery, LeaveRequestDto?>
{
    private readonly ILeaveRequestRepository _leaveRequestRepository;

    public GetLeaveRequestQueryHandler(ILeaveRequestRepository leaveRequestRepository)
    {
        _leaveRequestRepository = leaveRequestRepository;
    }

    public async Task<LeaveRequestDto?> Handle(GetLeaveRequestQuery request, CancellationToken cancellationToken)
    {
        var leaveRequest = await _leaveRequestRepository.GetByIdAsync(request.LeaveRequestId, cancellationToken);
        return leaveRequest is null ? null : SubmitLeaveRequestCommandHandler.MapToDto(leaveRequest);
    }
}

public class ListLeaveRequestsQueryHandler : IRequestHandler<ListLeaveRequestsQuery, PagedResult<LeaveRequestDto>>
{
    private readonly ILeaveRequestRepository _leaveRequestRepository;

    public ListLeaveRequestsQueryHandler(ILeaveRequestRepository leaveRequestRepository)
    {
        _leaveRequestRepository = leaveRequestRepository;
    }

    public async Task<PagedResult<LeaveRequestDto>> Handle(ListLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _leaveRequestRepository.ListAsync(
            request.EmployeeId,
            request.Status,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(SubmitLeaveRequestCommandHandler.MapToDto).ToList();
        return new PagedResult<LeaveRequestDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ─── PaySlip Query Handlers ────────────────────────────────────────────────

public class GetPaySlipQueryHandler : IRequestHandler<GetPaySlipQuery, PaySlipDto?>
{
    private readonly IPaySlipRepository _paySlipRepository;

    public GetPaySlipQueryHandler(IPaySlipRepository paySlipRepository)
    {
        _paySlipRepository = paySlipRepository;
    }

    public async Task<PaySlipDto?> Handle(GetPaySlipQuery request, CancellationToken cancellationToken)
    {
        var paySlip = await _paySlipRepository.GetByIdAsync(request.PaySlipId, cancellationToken);
        return paySlip is null ? null : GeneratePaySlipCommandHandler.MapToDto(paySlip);
    }
}

public class ListPaySlipsQueryHandler : IRequestHandler<ListPaySlipsQuery, PagedResult<PaySlipDto>>
{
    private readonly IPaySlipRepository _paySlipRepository;

    public ListPaySlipsQueryHandler(IPaySlipRepository paySlipRepository)
    {
        _paySlipRepository = paySlipRepository;
    }

    public async Task<PagedResult<PaySlipDto>> Handle(ListPaySlipsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _paySlipRepository.ListAsync(
            request.EmployeeId,
            request.Year,
            request.Month,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(GeneratePaySlipCommandHandler.MapToDto).ToList();
        return new PagedResult<PaySlipDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

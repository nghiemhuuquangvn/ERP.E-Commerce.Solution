using ERP.HRM.Application.Commands;
using ERP.HRM.Application.DTOs;
using ERP.HRM.Application.Interfaces;
using ERP.HRM.Domain.Entities;
using ERP.HRM.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.HRM.Application.Handlers;

// ─── Department Command Handlers ───────────────────────────────────────────

public class CreateDepartmentCommandHandler : IRequestHandler<CreateDepartmentCommand, DepartmentDto>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILogger<CreateDepartmentCommandHandler> _logger;

    public CreateDepartmentCommandHandler(
        IDepartmentRepository departmentRepository,
        ILogger<CreateDepartmentCommandHandler> logger)
    {
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    public async Task<DepartmentDto> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var codeUpper = request.Code.ToUpperInvariant();

        if (await _departmentRepository.ExistsByCodeAsync(codeUpper, cancellationToken))
            throw new InvalidOperationException($"A department with code '{codeUpper}' already exists.");

        var department = Department.Create(request.Name, codeUpper, request.ManagerId, request.ParentDepartmentId);
        _departmentRepository.Add(department);
        await _departmentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Department created: {DepartmentId} ({Code})", department.Id, department.Code);

        return MapToDto(department);
    }

    internal static DepartmentDto MapToDto(Department d) =>
        new(d.Id, d.DepartmentName, d.Code, d.ManagerId, d.ParentDepartmentId, d.CreatedAt, d.UpdatedAt);
}

public class UpdateDepartmentCommandHandler : IRequestHandler<UpdateDepartmentCommand, DepartmentDto>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILogger<UpdateDepartmentCommandHandler> _logger;

    public UpdateDepartmentCommandHandler(
        IDepartmentRepository departmentRepository,
        ILogger<UpdateDepartmentCommandHandler> logger)
    {
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    public async Task<DepartmentDto> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await _departmentRepository.GetByIdAsync(request.DepartmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Department {request.DepartmentId} not found.");

        department.Update(request.Name, request.Code.ToUpperInvariant(), request.ManagerId);
        _departmentRepository.Update(department);
        await _departmentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Department updated: {DepartmentId}", department.Id);

        return CreateDepartmentCommandHandler.MapToDto(department);
    }
}

// ─── Employee Command Handlers ─────────────────────────────────────────────

public class HireEmployeeCommandHandler : IRequestHandler<HireEmployeeCommand, EmployeeDto>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILogger<HireEmployeeCommandHandler> _logger;

    public HireEmployeeCommandHandler(
        IEmployeeRepository employeeRepository,
        IDepartmentRepository departmentRepository,
        ILogger<HireEmployeeCommandHandler> logger)
    {
        _employeeRepository = employeeRepository;
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    public async Task<EmployeeDto> Handle(HireEmployeeCommand request, CancellationToken cancellationToken)
    {
        _ = await _departmentRepository.GetByIdAsync(request.DepartmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Department {request.DepartmentId} not found.");

        if (!string.IsNullOrEmpty(request.Email) &&
            await _employeeRepository.ExistsByEmailAsync(request.Email, cancellationToken))
            throw new InvalidOperationException($"An employee with email '{request.Email}' already exists.");

        var year = request.DateOfHire.Year;
        var random4 = Random.Shared.Next(1000, 9999);
        var employeeNumber = $"EMP-{year}-{random4:D4}";

        var employee = Employee.Create(
            employeeNumber,
            request.FirstName,
            request.LastName,
            request.DepartmentId,
            request.DateOfHire,
            request.EmploymentType);

        employee.Email = request.Email;
        employee.Phone = request.Phone;

        employee.RaiseHiredEvent();

        _employeeRepository.Add(employee);
        await _employeeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee hired: {EmployeeId} ({EmployeeNumber})", employee.Id, employee.EmployeeNumber);

        return MapToDto(employee);
    }

    internal static EmployeeDto MapToDto(Employee e) =>
        new(e.Id,
            e.EmployeeNumber,
            e.FirstName,
            e.LastName,
            e.FullName,
            e.Email,
            e.Phone,
            e.DateOfHire,
            e.DepartmentId,
            ((EmploymentStatus)e.EmploymentStatus).ToString(),
            ((EmploymentType)e.EmploymentType).ToString(),
            e.CreatedAt,
            e.UpdatedAt);
}

public class TerminateEmployeeCommandHandler : IRequestHandler<TerminateEmployeeCommand, EmployeeDto>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILogger<TerminateEmployeeCommandHandler> _logger;

    public TerminateEmployeeCommandHandler(
        IEmployeeRepository employeeRepository,
        ILogger<TerminateEmployeeCommandHandler> logger)
    {
        _employeeRepository = employeeRepository;
        _logger = logger;
    }

    public async Task<EmployeeDto> Handle(TerminateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee {request.EmployeeId} not found.");

        employee.Terminate(request.Reason);
        _employeeRepository.Update(employee);
        await _employeeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee terminated: {EmployeeId}", employee.Id);

        return HireEmployeeCommandHandler.MapToDto(employee);
    }
}

public class UpdateEmployeeProfileCommandHandler : IRequestHandler<UpdateEmployeeProfileCommand, EmployeeDto>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILogger<UpdateEmployeeProfileCommandHandler> _logger;

    public UpdateEmployeeProfileCommandHandler(
        IEmployeeRepository employeeRepository,
        ILogger<UpdateEmployeeProfileCommandHandler> logger)
    {
        _employeeRepository = employeeRepository;
        _logger = logger;
    }

    public async Task<EmployeeDto> Handle(UpdateEmployeeProfileCommand request, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee {request.EmployeeId} not found.");

        if (!string.IsNullOrEmpty(request.Email) &&
            request.Email != employee.Email &&
            await _employeeRepository.ExistsByEmailAsync(request.Email, cancellationToken))
            throw new InvalidOperationException($"An employee with email '{request.Email}' already exists.");

        employee.UpdateProfile(request.Email, request.Phone);
        _employeeRepository.Update(employee);
        await _employeeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee profile updated: {EmployeeId}", employee.Id);

        return HireEmployeeCommandHandler.MapToDto(employee);
    }
}

public class TransferEmployeeCommandHandler : IRequestHandler<TransferEmployeeCommand, EmployeeDto>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILogger<TransferEmployeeCommandHandler> _logger;

    public TransferEmployeeCommandHandler(
        IEmployeeRepository employeeRepository,
        IDepartmentRepository departmentRepository,
        ILogger<TransferEmployeeCommandHandler> logger)
    {
        _employeeRepository = employeeRepository;
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    public async Task<EmployeeDto> Handle(TransferEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee {request.EmployeeId} not found.");

        _ = await _departmentRepository.GetByIdAsync(request.NewDepartmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Department {request.NewDepartmentId} not found.");

        if (employee.DepartmentId == request.NewDepartmentId)
            throw new InvalidOperationException("Employee is already in the specified department.");

        employee.Transfer(request.NewDepartmentId);
        _employeeRepository.Update(employee);
        await _employeeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Employee {EmployeeId} transferred to department {DepartmentId}",
            employee.Id, request.NewDepartmentId);

        return HireEmployeeCommandHandler.MapToDto(employee);
    }
}

// ─── Leave Request Command Handlers ───────────────────────────────────────

public class SubmitLeaveRequestCommandHandler : IRequestHandler<SubmitLeaveRequestCommand, LeaveRequestDto>
{
    private readonly ILeaveRequestRepository _leaveRequestRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILogger<SubmitLeaveRequestCommandHandler> _logger;

    public SubmitLeaveRequestCommandHandler(
        ILeaveRequestRepository leaveRequestRepository,
        IEmployeeRepository employeeRepository,
        ILogger<SubmitLeaveRequestCommandHandler> logger)
    {
        _leaveRequestRepository = leaveRequestRepository;
        _employeeRepository = employeeRepository;
        _logger = logger;
    }

    public async Task<LeaveRequestDto> Handle(SubmitLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee {request.EmployeeId} not found.");

        if (employee.EmploymentStatus == (int)EmploymentStatus.Terminated)
            throw new InvalidOperationException("Cannot submit leave request for a terminated employee.");

        var daysRequested = (int)(request.EndDate.Date - request.StartDate.Date).TotalDays + 1;
        var year = request.StartDate.Year;
        var random4 = Random.Shared.Next(1000, 9999);
        var leaveRequestNumber = $"LR-{year}-{random4:D4}";

        var leaveRequest = LeaveRequest.Create(
            leaveRequestNumber,
            request.EmployeeId,
            request.LeaveType,
            request.StartDate,
            request.EndDate,
            daysRequested,
            request.Reason);

        _leaveRequestRepository.Add(leaveRequest);
        await _leaveRequestRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Leave request submitted: {LeaveRequestId} for employee {EmployeeId}",
            leaveRequest.Id, request.EmployeeId);

        return MapToDto(leaveRequest);
    }

    internal static LeaveRequestDto MapToDto(LeaveRequest lr) =>
        new(lr.Id,
            lr.LeaveRequestNumber,
            lr.EmployeeId,
            ((LeaveType)lr.LeaveTypeId).ToString(),
            lr.StartDate,
            lr.EndDate,
            lr.DaysRequested,
            ((LeaveStatus)lr.LeaveStatus).ToString(),
            lr.Reason,
            lr.CreatedAt,
            lr.UpdatedAt);
}

public class ApproveLeaveCommandHandler : IRequestHandler<ApproveLeaveCommand, LeaveRequestDto>
{
    private readonly ILeaveRequestRepository _leaveRequestRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILogger<ApproveLeaveCommandHandler> _logger;

    public ApproveLeaveCommandHandler(
        ILeaveRequestRepository leaveRequestRepository,
        IEmployeeRepository employeeRepository,
        ILogger<ApproveLeaveCommandHandler> logger)
    {
        _leaveRequestRepository = leaveRequestRepository;
        _employeeRepository = employeeRepository;
        _logger = logger;
    }

    public async Task<LeaveRequestDto> Handle(ApproveLeaveCommand request, CancellationToken cancellationToken)
    {
        var leaveRequest = await _leaveRequestRepository.GetByIdAsync(request.LeaveRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave request {request.LeaveRequestId} not found.");

        leaveRequest.Approve();

        // Set employee to OnLeave status if leave starts today or in the past
        if (leaveRequest.StartDate.Date <= DateTime.UtcNow.Date)
        {
            var employee = await _employeeRepository.GetByIdAsync(leaveRequest.EmployeeId, cancellationToken);
            if (employee != null && employee.EmploymentStatus == (int)EmploymentStatus.Active)
            {
                employee.SetOnLeave();
                _employeeRepository.Update(employee);
            }
        }

        _leaveRequestRepository.Update(leaveRequest);
        await _leaveRequestRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Leave request approved: {LeaveRequestId}", leaveRequest.Id);

        return SubmitLeaveRequestCommandHandler.MapToDto(leaveRequest);
    }
}

public class RejectLeaveCommandHandler : IRequestHandler<RejectLeaveCommand, LeaveRequestDto>
{
    private readonly ILeaveRequestRepository _leaveRequestRepository;
    private readonly ILogger<RejectLeaveCommandHandler> _logger;

    public RejectLeaveCommandHandler(
        ILeaveRequestRepository leaveRequestRepository,
        ILogger<RejectLeaveCommandHandler> logger)
    {
        _leaveRequestRepository = leaveRequestRepository;
        _logger = logger;
    }

    public async Task<LeaveRequestDto> Handle(RejectLeaveCommand request, CancellationToken cancellationToken)
    {
        var leaveRequest = await _leaveRequestRepository.GetByIdAsync(request.LeaveRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave request {request.LeaveRequestId} not found.");

        leaveRequest.Reject(request.Reason);
        _leaveRequestRepository.Update(leaveRequest);
        await _leaveRequestRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Leave request rejected: {LeaveRequestId}", leaveRequest.Id);

        return SubmitLeaveRequestCommandHandler.MapToDto(leaveRequest);
    }
}

public class CancelLeaveCommandHandler : IRequestHandler<CancelLeaveCommand, LeaveRequestDto>
{
    private readonly ILeaveRequestRepository _leaveRequestRepository;
    private readonly ILogger<CancelLeaveCommandHandler> _logger;

    public CancelLeaveCommandHandler(
        ILeaveRequestRepository leaveRequestRepository,
        ILogger<CancelLeaveCommandHandler> logger)
    {
        _leaveRequestRepository = leaveRequestRepository;
        _logger = logger;
    }

    public async Task<LeaveRequestDto> Handle(CancelLeaveCommand request, CancellationToken cancellationToken)
    {
        var leaveRequest = await _leaveRequestRepository.GetByIdAsync(request.LeaveRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave request {request.LeaveRequestId} not found.");

        leaveRequest.Cancel();
        _leaveRequestRepository.Update(leaveRequest);
        await _leaveRequestRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Leave request cancelled: {LeaveRequestId}", leaveRequest.Id);

        return SubmitLeaveRequestCommandHandler.MapToDto(leaveRequest);
    }
}

// ─── Payroll Command Handlers ──────────────────────────────────────────────

public class GeneratePaySlipCommandHandler : IRequestHandler<GeneratePaySlipCommand, PaySlipDto>
{
    private readonly IPaySlipRepository _paySlipRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILogger<GeneratePaySlipCommandHandler> _logger;

    public GeneratePaySlipCommandHandler(
        IPaySlipRepository paySlipRepository,
        IEmployeeRepository employeeRepository,
        ILogger<GeneratePaySlipCommandHandler> logger)
    {
        _paySlipRepository = paySlipRepository;
        _employeeRepository = employeeRepository;
        _logger = logger;
    }

    public async Task<PaySlipDto> Handle(GeneratePaySlipCommand request, CancellationToken cancellationToken)
    {
        _ = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee {request.EmployeeId} not found.");

        if (await _paySlipRepository.ExistsForPeriodAsync(request.EmployeeId, request.Year, request.Month, cancellationToken))
            throw new InvalidOperationException(
                $"Pay slip for employee {request.EmployeeId} for period {request.Year:D4}-{request.Month:D2} already exists.");

        var paySlipNumber = $"PS-{request.Year:D4}{request.Month:D2}-{Random.Shared.Next(1000, 9999):D4}";

        var paySlip = PaySlip.Create(
            paySlipNumber,
            request.EmployeeId,
            request.Year,
            request.Month,
            request.GrossPay,
            request.Deductions);

        _paySlipRepository.Add(paySlip);
        await _paySlipRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Pay slip generated: {PaySlipId} for employee {EmployeeId} period {Period}",
            paySlip.Id, request.EmployeeId, paySlip.PayPeriod);

        return MapToDto(paySlip);
    }

    internal static PaySlipDto MapToDto(PaySlip ps) =>
        new(ps.Id,
            ps.PaySlipNumber,
            ps.EmployeeId,
            ps.Year,
            ps.Month,
            ps.PayPeriod,
            ps.GrossSalary,
            ps.Deductions,
            ps.NetSalary,
            ps.PaymentDate,
            ((Domain.Enums.PaymentStatus)ps.PaymentStatus).ToString(),
            ps.CreatedAt);
}

public class ProcessPayrollCommandHandler : IRequestHandler<ProcessPayrollCommand, List<PaySlipDto>>
{
    private readonly IPaySlipRepository _paySlipRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILogger<ProcessPayrollCommandHandler> _logger;

    // Default deduction rate (30% for taxes and benefits)
    private const decimal DefaultDeductionRate = 0.30m;

    // Default gross salary when not set (fallback for demo/seeding)
    private const decimal DefaultGrossSalary = 5000m;

    public ProcessPayrollCommandHandler(
        IPaySlipRepository paySlipRepository,
        IEmployeeRepository employeeRepository,
        ILogger<ProcessPayrollCommandHandler> logger)
    {
        _paySlipRepository = paySlipRepository;
        _employeeRepository = employeeRepository;
        _logger = logger;
    }

    public async Task<List<PaySlipDto>> Handle(ProcessPayrollCommand request, CancellationToken cancellationToken)
    {
        var activeEmployees = await _employeeRepository.GetActiveEmployeesAsync(cancellationToken);

        _logger.LogInformation(
            "Processing payroll for {Year}-{Month:D2} — {Count} active employees",
            request.Year, request.Month, activeEmployees.Count);

        var results = new List<PaySlipDto>();

        foreach (var employee in activeEmployees)
        {
            if (await _paySlipRepository.ExistsForPeriodAsync(employee.Id, request.Year, request.Month, cancellationToken))
            {
                _logger.LogWarning(
                    "Pay slip already exists for employee {EmployeeId} period {Year}-{Month:D2} — skipping.",
                    employee.Id, request.Year, request.Month);
                continue;
            }

            var grossSalary = DefaultGrossSalary;
            var deductions = Math.Round(grossSalary * DefaultDeductionRate, 2);

            var paySlipNumber = $"PS-{request.Year:D4}{request.Month:D2}-{Random.Shared.Next(1000, 9999):D4}";

            var paySlip = PaySlip.Create(
                paySlipNumber,
                employee.Id,
                request.Year,
                request.Month,
                grossSalary,
                deductions);

            _paySlipRepository.Add(paySlip);
            results.Add(GeneratePaySlipCommandHandler.MapToDto(paySlip));
        }

        await _paySlipRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Payroll processed for {Year}-{Month:D2} — {Count} pay slips generated.",
            request.Year, request.Month, results.Count);

        return results;
    }
}

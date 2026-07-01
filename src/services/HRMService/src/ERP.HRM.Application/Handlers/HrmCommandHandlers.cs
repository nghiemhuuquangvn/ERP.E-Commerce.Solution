using ERP.HRM.Application.Commands;
using ERP.HRM.Application.DTOs;
using ERP.HRM.Application.Interfaces;
using ERP.HRM.Domain.Entities;
using ERP.HRM.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.HRM.Application.Handlers;

// Department/Employee command handlers moved to SharedMasterDataService.

// ─── Leave Request Command Handlers ───────────────────────────────────────

public class SubmitLeaveRequestCommandHandler : IRequestHandler<SubmitLeaveRequestCommand, LeaveRequestDto>
{
    private readonly ILeaveRequestRepository _leaveRequestRepository;
    private readonly IEmployeeSnapshotRepository _employeeSnapshotRepository;
    private readonly ILogger<SubmitLeaveRequestCommandHandler> _logger;

    public SubmitLeaveRequestCommandHandler(
        ILeaveRequestRepository leaveRequestRepository,
        IEmployeeSnapshotRepository employeeSnapshotRepository,
        ILogger<SubmitLeaveRequestCommandHandler> logger)
    {
        _leaveRequestRepository = leaveRequestRepository;
        _employeeSnapshotRepository = employeeSnapshotRepository;
        _logger = logger;
    }

    public async Task<LeaveRequestDto> Handle(SubmitLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        // Local snapshot is a passive projection synced from SharedMasterDataService's
        // Kafka events, not a source of truth — if it hasn't caught up yet (or the
        // event hasn't arrived), trust the caller-supplied EmployeeId rather than
        // hard-failing (matches this codebase's loose cross-service coupling).
        var snapshot = await _employeeSnapshotRepository.GetByIdAsync(request.EmployeeId, cancellationToken);

        if (snapshot != null && snapshot.EmploymentStatus == (int)EmploymentStatus.Terminated)
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
    private readonly IEmployeeSnapshotRepository _employeeSnapshotRepository;
    private readonly ILogger<ApproveLeaveCommandHandler> _logger;

    public ApproveLeaveCommandHandler(
        ILeaveRequestRepository leaveRequestRepository,
        IEmployeeSnapshotRepository employeeSnapshotRepository,
        ILogger<ApproveLeaveCommandHandler> logger)
    {
        _leaveRequestRepository = leaveRequestRepository;
        _employeeSnapshotRepository = employeeSnapshotRepository;
        _logger = logger;
    }

    public async Task<LeaveRequestDto> Handle(ApproveLeaveCommand request, CancellationToken cancellationToken)
    {
        var leaveRequest = await _leaveRequestRepository.GetByIdAsync(request.LeaveRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave request {request.LeaveRequestId} not found.");

        leaveRequest.Approve();

        // HRM no longer owns the Employee aggregate — it can only update its own
        // local snapshot optimistically here. SharedMasterDataService closes the
        // loop by consuming LeaveApprovedEvent/LeaveRejectedEvent (published via
        // HRM's own outbox) and setting the canonical Employee's status itself.
        if (leaveRequest.StartDate.Date <= DateTime.UtcNow.Date)
        {
            var snapshot = await _employeeSnapshotRepository.GetByIdAsync(leaveRequest.EmployeeId, cancellationToken);
            if (snapshot != null && snapshot.EmploymentStatus == (int)EmploymentStatus.Active)
            {
                snapshot.EmploymentStatus = (int)EmploymentStatus.OnLeave;
                snapshot.UpdatedAt = DateTime.UtcNow;
                _employeeSnapshotRepository.Upsert(snapshot);
            }
        }

        _leaveRequestRepository.Update(leaveRequest);
        await _leaveRequestRepository.SaveChangesAsync(cancellationToken);
        await _employeeSnapshotRepository.SaveChangesAsync(cancellationToken);

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
    private readonly IEmployeeSnapshotRepository _employeeSnapshotRepository;
    private readonly ILogger<GeneratePaySlipCommandHandler> _logger;

    public GeneratePaySlipCommandHandler(
        IPaySlipRepository paySlipRepository,
        IEmployeeSnapshotRepository employeeSnapshotRepository,
        ILogger<GeneratePaySlipCommandHandler> logger)
    {
        _paySlipRepository = paySlipRepository;
        _employeeSnapshotRepository = employeeSnapshotRepository;
        _logger = logger;
    }

    public async Task<PaySlipDto> Handle(GeneratePaySlipCommand request, CancellationToken cancellationToken)
    {
        // See SubmitLeaveRequestCommandHandler: snapshot absence isn't fatal, only used
        // when present to avoid generating pay slips for known-terminated employees.
        var snapshot = await _employeeSnapshotRepository.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (snapshot != null && snapshot.EmploymentStatus == (int)EmploymentStatus.Terminated)
            throw new InvalidOperationException($"Employee {request.EmployeeId} is terminated.");

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
    private readonly IEmployeeSnapshotRepository _employeeSnapshotRepository;
    private readonly ILogger<ProcessPayrollCommandHandler> _logger;

    // Default deduction rate (30% for taxes and benefits)
    private const decimal DefaultDeductionRate = 0.30m;

    // Default gross salary when not set (fallback for demo/seeding)
    private const decimal DefaultGrossSalary = 5000m;

    public ProcessPayrollCommandHandler(
        IPaySlipRepository paySlipRepository,
        IEmployeeSnapshotRepository employeeSnapshotRepository,
        ILogger<ProcessPayrollCommandHandler> logger)
    {
        _paySlipRepository = paySlipRepository;
        _employeeSnapshotRepository = employeeSnapshotRepository;
        _logger = logger;
    }

    public async Task<List<PaySlipDto>> Handle(ProcessPayrollCommand request, CancellationToken cancellationToken)
    {
        var activeEmployees = await _employeeSnapshotRepository.GetActiveAsync(cancellationToken);

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

using ERP.HRM.Application.Commands;
using ERP.HRM.Application.DTOs;
using ERP.HRM.Application.Interfaces;
using ERP.HRM.Domain.Entities;
using ERP.HRM.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

// OT rate: 1.5× hourly rate. Hourly rate = GrossSalary / 160 (standard monthly hours).

namespace ERP.HRM.Application.Handlers;

// Department/Employee command handlers moved to SharedMasterDataService.

// ─── Leave Request Command Handlers ───────────────────────────────────────

public class SubmitLeaveRequestCommandHandler : IRequestHandler<SubmitLeaveRequestCommand, LeaveRequestDto>
{
    private readonly IHrmUnitOfWork _uow;
    private readonly ILogger<SubmitLeaveRequestCommandHandler> _logger;

    public SubmitLeaveRequestCommandHandler(
        IHrmUnitOfWork uow,
        ILogger<SubmitLeaveRequestCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<LeaveRequestDto> Handle(SubmitLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        // Local snapshot is a passive projection synced from SharedMasterDataService's
        // Kafka events, not a source of truth — if it hasn't caught up yet (or the
        // event hasn't arrived), trust the caller-supplied EmployeeId rather than
        // hard-failing (matches this codebase's loose cross-service coupling).
        var snapshot = await _uow.EmployeeSnapshots.GetByIdAsync(request.EmployeeId, cancellationToken);

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

        _uow.LeaveRequests.Add(leaveRequest);
        await _uow.SaveChangesAsync(cancellationToken);

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
    private readonly IHrmUnitOfWork _uow;
    private readonly ILogger<ApproveLeaveCommandHandler> _logger;

    public ApproveLeaveCommandHandler(
        IHrmUnitOfWork uow,
        ILogger<ApproveLeaveCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<LeaveRequestDto> Handle(ApproveLeaveCommand request, CancellationToken cancellationToken)
    {
        var leaveRequest = await _uow.LeaveRequests.GetByIdAsync(request.LeaveRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave request {request.LeaveRequestId} not found.");

        leaveRequest.Approve();

        // HRM no longer owns the Employee aggregate — it can only update its own
        // local snapshot optimistically here. SharedMasterDataService closes the
        // loop by consuming LeaveApprovedEvent/LeaveRejectedEvent (published via
        // HRM's own outbox) and setting the canonical Employee's status itself.
        if (leaveRequest.StartDate.Date <= DateTime.UtcNow.Date)
        {
            var snapshot = await _uow.EmployeeSnapshots.GetByIdAsync(leaveRequest.EmployeeId, cancellationToken);
            if (snapshot != null && snapshot.EmploymentStatus == (int)EmploymentStatus.Active)
            {
                snapshot.EmploymentStatus = (int)EmploymentStatus.OnLeave;
                snapshot.UpdatedAt = DateTime.UtcNow;
                _uow.EmployeeSnapshots.Upsert(snapshot);
            }
        }

        _uow.LeaveRequests.Update(leaveRequest);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Leave request approved: {LeaveRequestId}", leaveRequest.Id);

        return SubmitLeaveRequestCommandHandler.MapToDto(leaveRequest);
    }
}

public class RejectLeaveCommandHandler : IRequestHandler<RejectLeaveCommand, LeaveRequestDto>
{
    private readonly IHrmUnitOfWork _uow;
    private readonly ILogger<RejectLeaveCommandHandler> _logger;

    public RejectLeaveCommandHandler(
        IHrmUnitOfWork uow,
        ILogger<RejectLeaveCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<LeaveRequestDto> Handle(RejectLeaveCommand request, CancellationToken cancellationToken)
    {
        var leaveRequest = await _uow.LeaveRequests.GetByIdAsync(request.LeaveRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave request {request.LeaveRequestId} not found.");

        leaveRequest.Reject(request.Reason);
        _uow.LeaveRequests.Update(leaveRequest);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Leave request rejected: {LeaveRequestId}", leaveRequest.Id);

        return SubmitLeaveRequestCommandHandler.MapToDto(leaveRequest);
    }
}

public class CancelLeaveCommandHandler : IRequestHandler<CancelLeaveCommand, LeaveRequestDto>
{
    private readonly IHrmUnitOfWork _uow;
    private readonly ILogger<CancelLeaveCommandHandler> _logger;

    public CancelLeaveCommandHandler(
        IHrmUnitOfWork uow,
        ILogger<CancelLeaveCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<LeaveRequestDto> Handle(CancelLeaveCommand request, CancellationToken cancellationToken)
    {
        var leaveRequest = await _uow.LeaveRequests.GetByIdAsync(request.LeaveRequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave request {request.LeaveRequestId} not found.");

        leaveRequest.Cancel();
        _uow.LeaveRequests.Update(leaveRequest);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Leave request cancelled: {LeaveRequestId}", leaveRequest.Id);

        return SubmitLeaveRequestCommandHandler.MapToDto(leaveRequest);
    }
}

// ─── Payroll Command Handlers ──────────────────────────────────────────────

public class GeneratePaySlipCommandHandler : IRequestHandler<GeneratePaySlipCommand, PaySlipDto>
{
    private readonly IHrmUnitOfWork _uow;
    private readonly ILogger<GeneratePaySlipCommandHandler> _logger;

    public GeneratePaySlipCommandHandler(
        IHrmUnitOfWork uow,
        ILogger<GeneratePaySlipCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<PaySlipDto> Handle(GeneratePaySlipCommand request, CancellationToken cancellationToken)
    {
        var snapshot = await _uow.EmployeeSnapshots.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (snapshot != null && snapshot.EmploymentStatus == (int)EmploymentStatus.Terminated)
            throw new InvalidOperationException($"Employee {request.EmployeeId} is terminated.");

        if (await _uow.PaySlips.ExistsForPeriodAsync(request.EmployeeId, request.Year, request.Month, cancellationToken))
            throw new InvalidOperationException(
                $"Pay slip for employee {request.EmployeeId} for period {request.Year:D4}-{request.Month:D2} already exists.");

        var paySlipNumber = $"PS-{request.Year:D4}{request.Month:D2}-{Random.Shared.Next(1000, 9999):D4}";

        var paySlip = PaySlip.Create(
            paySlipNumber,
            request.EmployeeId,
            request.Year,
            request.Month,
            request.GrossPay,
            request.Deductions,
            request.OvertimeHours,
            request.OvertimePay);

        _uow.PaySlips.Add(paySlip);
        await _uow.SaveChangesAsync(cancellationToken);

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
            ps.OvertimeHours,
            ps.OvertimePay,
            ps.NetSalary,
            ps.PaymentDate,
            ((Domain.Enums.PaymentStatus)ps.PaymentStatus).ToString(),
            ps.CreatedAt);
}

public class PayPaySlipCommandHandler : IRequestHandler<PayPaySlipCommand, PaySlipDto>
{
    private readonly IHrmUnitOfWork _uow;
    public PayPaySlipCommandHandler(IHrmUnitOfWork uow) => _uow = uow;

    public async Task<PaySlipDto> Handle(PayPaySlipCommand request, CancellationToken cancellationToken)
    {
        var paySlip = await _uow.PaySlips.GetByIdAsync(request.PaySlipId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pay slip {request.PaySlipId} not found.");

        try { paySlip.MarkPaid(request.PaidOn ?? DateTime.UtcNow, request.PaymentMethod); }
        catch (InvalidOperationException ex) { throw new BusinessRuleException("HRM", ex.Message); }

        await _uow.SaveChangesAsync(cancellationToken);
        return GeneratePaySlipCommandHandler.MapToDto(paySlip);
    }
}

public class CancelPaySlipCommandHandler : IRequestHandler<CancelPaySlipCommand, PaySlipDto>
{
    private readonly IHrmUnitOfWork _uow;
    public CancelPaySlipCommandHandler(IHrmUnitOfWork uow) => _uow = uow;

    public async Task<PaySlipDto> Handle(CancelPaySlipCommand request, CancellationToken cancellationToken)
    {
        var paySlip = await _uow.PaySlips.GetByIdAsync(request.PaySlipId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pay slip {request.PaySlipId} not found.");

        try { paySlip.Cancel(request.Reason); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { throw new BusinessRuleException("HRM", ex.Message); }

        await _uow.SaveChangesAsync(cancellationToken);
        return GeneratePaySlipCommandHandler.MapToDto(paySlip);
    }
}

public class ProcessPayrollCommandHandler : IRequestHandler<ProcessPayrollCommand, List<PaySlipDto>>
{
    private readonly IHrmUnitOfWork _uow;
    private readonly ILogger<ProcessPayrollCommandHandler> _logger;

    private const decimal DefaultDeductionRate = 0.30m;
    private const decimal DefaultGrossSalary = 5000m;
    private const decimal StandardMonthlyHours = 160m;
    private const decimal OvertimeRateMultiplier = 1.5m;

    public ProcessPayrollCommandHandler(
        IHrmUnitOfWork uow,
        ILogger<ProcessPayrollCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<List<PaySlipDto>> Handle(ProcessPayrollCommand request, CancellationToken cancellationToken)
    {
        var activeEmployees = await _uow.EmployeeSnapshots.GetActiveAsync(cancellationToken);

        _logger.LogInformation(
            "Processing payroll for {Year}-{Month:D2} — {Count} active employees",
            request.Year, request.Month, activeEmployees.Count);

        var results = new List<PaySlipDto>();

        foreach (var employee in activeEmployees)
        {
            if (await _uow.PaySlips.ExistsForPeriodAsync(employee.Id, request.Year, request.Month, cancellationToken))
            {
                _logger.LogWarning(
                    "Pay slip already exists for employee {EmployeeId} period {Year}-{Month:D2} — skipping.",
                    employee.Id, request.Year, request.Month);
                continue;
            }

            var grossSalary = DefaultGrossSalary;
            var deductions = Math.Round(grossSalary * DefaultDeductionRate, 2);

            var overtimeHours = await _uow.Attendance.GetMonthlyOvertimeHoursAsync(
                employee.Id, request.Year, request.Month, cancellationToken);

            var hourlyRate = grossSalary / StandardMonthlyHours;
            var overtimePay = Math.Round(overtimeHours * hourlyRate * OvertimeRateMultiplier, 2);

            var paySlipNumber = $"PS-{request.Year:D4}{request.Month:D2}-{Random.Shared.Next(1000, 9999):D4}";

            var paySlip = PaySlip.Create(
                paySlipNumber,
                employee.Id,
                request.Year,
                request.Month,
                grossSalary,
                deductions,
                overtimeHours,
                overtimePay);

            _uow.PaySlips.Add(paySlip);
            results.Add(GeneratePaySlipCommandHandler.MapToDto(paySlip));
        }

        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Payroll processed for {Year}-{Month:D2} — {Count} pay slips generated.",
            request.Year, request.Month, results.Count);

        return results;
    }
}

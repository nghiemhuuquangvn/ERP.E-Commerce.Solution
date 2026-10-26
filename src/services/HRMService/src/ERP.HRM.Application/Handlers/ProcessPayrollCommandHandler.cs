using ERP.HRM.Application.Commands;
using ERP.HRM.Application.Interfaces;
using ERP.HRM.Domain.Entities;
using ERP.HRM.Domain.Enums;
using ERP.HRM.Domain.Payroll;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.HRM.Application.Handlers;

/// <summary>
/// Monthly payroll (ROADMAP P1-7): for every active employee without a pay slip for the period, the salary terms, dependents,
/// worked days, overtime and the rate set in force make one calculated pay slip. An employee without salary terms is skipped
/// and reported, never given invented figures.
/// </summary>
public class ProcessPayrollCommandHandler : IRequestHandler<ProcessPayrollCommand, ProcessPayrollResultDto>
{
    private readonly IHrmUnitOfWork _uow;
    private readonly PayrollSupport _support;
    private readonly ILogger<ProcessPayrollCommandHandler> _logger;

    public ProcessPayrollCommandHandler(IHrmUnitOfWork uow, PayrollSupport support, ILogger<ProcessPayrollCommandHandler> logger)
    {
        _uow = uow;
        _support = support;
        _logger = logger;
    }

    public async Task<ProcessPayrollResultDto> Handle(ProcessPayrollCommand request, CancellationToken cancellationToken)
    {
        if (request.Month is < 1 or > 12 || request.Year is < 2000 or > 2100)
            throw new BusinessRuleException("HRM", "Choose a valid year and month.");

        var periodStart = new DateTime(request.Year, request.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        var rateSet = await _support.RateSetAsync(periodEnd, cancellationToken)
            ?? throw new BusinessRuleException("HRM", $"No payroll rate set is in force for {request.Year:D4}-{request.Month:D2}. Add one in Payroll settings.");
        var rules = rateSet.ToRules();
        var standardDays = PayrollCalculator.StandardWorkingDays(request.Year, request.Month);

        var activeEmployees = await _uow.EmployeeSnapshots.GetActiveAsync(cancellationToken);
        _logger.LogInformation("Processing payroll for {Year}-{Month:D2} - {Count} active employees", request.Year, request.Month, activeEmployees.Count);

        var created = new List<DTOs.PaySlipDto>();
        var skipped = new List<SkippedEmployeeDto>();

        foreach (var employee in activeEmployees)
        {
            if (await _uow.PaySlips.ExistsForPeriodAsync(employee.Id, request.Year, request.Month, cancellationToken))
                continue; // already has a (non-cancelled) pay slip for the period

            var terms = await _uow.Payroll.GetCompensationAsync(employee.Id, periodEnd, cancellationToken);
            if (terms == null)
            {
                skipped.Add(new SkippedEmployeeDto(employee.Id, employee.EmployeeNumber, employee.FullName, "No salary profile in force for the period."));
                continue;
            }

            var dependents = (await _uow.Payroll.ListDependentsAsync(employee.Id, cancellationToken))
                .Count(d => d.CoversPeriod(request.Year, request.Month));

            var paidDays = await PaidDaysAsync(employee.Id, request.Year, request.Month, standardDays, periodStart, periodEnd, cancellationToken);
            var overtimeHours = await _uow.Attendance.GetMonthlyOvertimeHoursAsync(employee.Id, request.Year, request.Month, cancellationToken);

            var result = PayrollCalculator.Calculate(new PayrollInput(
                terms.BaseSalary, terms.TaxableAllowance, terms.ExemptAllowance, terms.InsuranceSalary, terms.InsuranceEnabled,
                dependents, paidDays, standardDays, overtimeHours), rules);

            var number = $"PS-{request.Year:D4}{request.Month:D2}-{Random.Shared.Next(1000, 9999):D4}";
            var paySlip = PaySlip.CreateCalculated(number, employee.Id, request.Year, request.Month, result);

            _uow.PaySlips.Add(paySlip);
            created.Add(GeneratePaySlipCommandHandler.MapToDto(paySlip));
        }

        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Payroll processed for {Year}-{Month:D2} - {Created} pay slips generated, {Skipped} employees skipped.",
            request.Year, request.Month, created.Count, skipped.Count);

        return new ProcessPayrollResultDto(created, skipped);
    }

    /// <summary>
    /// Standard days minus the days not worked. A month with no attendance records at all means the tenant does not track
    /// attendance, so the full month is paid; otherwise Absent days, half of the HalfDay days and approved unpaid leave are taken off.
    /// </summary>
    private async Task<decimal> PaidDaysAsync(Guid employeeId, int year, int month, int standardDays, DateTime from, DateTime to, CancellationToken ct)
    {
        decimal notWorked = 0m;

        var (records, absent, half) = await _uow.Attendance.GetMonthlyCountsAsync(employeeId, year, month, ct);
        if (records > 0) notWorked += absent + half * 0.5m;

        var unpaid = await _uow.LeaveRequests.GetApprovedOverlappingAsync(employeeId, (int)LeaveType.Unpaid, from, to, ct);
        notWorked += unpaid.Sum(l => PayrollSupport.WorkingDays(l.StartDate, l.EndDate, from, to));

        return Math.Max(0m, standardDays - notWorked);
    }
}

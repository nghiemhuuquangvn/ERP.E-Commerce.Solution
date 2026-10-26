using ERP.HRM.Application.Commands;
using ERP.HRM.Application.DTOs;
using ERP.HRM.Application.Interfaces;
using ERP.HRM.Domain.Entities;
using ERP.HRM.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.HRM.Application.Handlers;

/// <summary>Shared lookups of the payroll handlers: the rate set in force and the annual-leave balance.</summary>
public class PayrollSupport
{
    private readonly IHrmUnitOfWork _uow;

    public PayrollSupport(IHrmUnitOfWork uow) => _uow = uow;

    /// <summary>
    /// The rate set in force on a date. A tenant that has none at all gets the default (unconfirmed) set, so payroll can run on day one;
    /// an accountant then confirms or replaces it.
    /// </summary>
    public async Task<PayrollRateSet?> RateSetAsync(DateTime asOf, CancellationToken ct)
    {
        var set = await _uow.Payroll.GetRateSetAsync(asOf, ct);
        if (set != null || await _uow.Payroll.AnyRateSetAsync(ct)) return set;

        set = DefaultPayrollRates.Create();
        _uow.Payroll.Add(set);
        foreach (var b in set.Brackets) _uow.Payroll.Add(b);
        return set.EffectiveFrom <= asOf ? set : null;
    }

    public async Task<LeaveBalance> AnnualBalanceAsync(Guid employeeId, int year, CancellationToken ct)
    {
        var balance = await _uow.Payroll.GetLeaveBalanceAsync(employeeId, year, (int)LeaveType.Annual, ct);
        if (balance != null) return balance;

        var rateSet = await RateSetAsync(new DateTime(year, 12, 31), ct);
        balance = new LeaveBalance
        {
            EmployeeId = employeeId,
            Year = year,
            LeaveType = (int)LeaveType.Annual,
            TotalDays = rateSet?.AnnualLeaveDays ?? 12m
        };
        _uow.Payroll.Add(balance);
        return balance;
    }

    /// <summary>Monday-Friday days of the leave that fall inside [from, to].</summary>
    public static int WorkingDays(DateTime start, DateTime end, DateTime? from = null, DateTime? to = null)
    {
        var s = from.HasValue && from.Value.Date > start.Date ? from.Value.Date : start.Date;
        var e = to.HasValue && to.Value.Date < end.Date ? to.Value.Date : end.Date;
        var days = 0;
        for (var d = s; d <= e; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days++;
        return days;
    }
}

/// <summary>Insurance rates, deductions, caps and tax brackets in force from 1 Jan 2026 as the project understands them. Not reviewed by an accountant.</summary>
public static class DefaultPayrollRates
{
    public static PayrollRateSet Create()
    {
        var set = new PayrollRateSet
        {
            Id = Guid.NewGuid(),
            EffectiveFrom = new DateTime(2026, 1, 1),
            Name = "Default rates (to be confirmed by an accountant)",
            SocialEmployeeRate = 0.08m,
            HealthEmployeeRate = 0.015m,
            UnemploymentEmployeeRate = 0.01m,
            SocialEmployerRate = 0.175m,
            HealthEmployerRate = 0.03m,
            UnemploymentEmployerRate = 0.01m,
            PersonalDeduction = 15_500_000m,
            DependentDeduction = 6_200_000m,
            InsuranceSalaryCap = 46_800_000m,
            UnemploymentSalaryCap = 106_200_000m,
            AnnualLeaveDays = 12m,
            IsConfirmed = false
        };
        foreach (var (upTo, rate) in new (decimal?, decimal)[] { (10_000_000m, 0.05m), (30_000_000m, 0.10m), (60_000_000m, 0.20m), (100_000_000m, 0.30m), (null, 0.35m) })
            set.Brackets.Add(new PitBracket { Id = Guid.NewGuid(), RateSetId = set.Id, UpTo = upTo, Rate = rate });
        return set;
    }
}

// ─── Salary terms ─────────────────────────────────────────────────────────

public class SaveCompensationCommandHandler : IRequestHandler<SaveCompensationCommand, CompensationDto>
{
    private readonly IHrmUnitOfWork _uow;
    public SaveCompensationCommandHandler(IHrmUnitOfWork uow) => _uow = uow;

    public async Task<CompensationDto> Handle(SaveCompensationCommand r, CancellationToken ct)
    {
        if (r.BaseSalary <= 0) throw new BusinessRuleException("HRM", "The base salary must be greater than zero.");
        if (r.TaxableAllowance < 0 || r.ExemptAllowance < 0) throw new BusinessRuleException("HRM", "Allowances cannot be negative.");
        if (r.InsuranceSalary is < 0) throw new BusinessRuleException("HRM", "The insurance salary cannot be negative.");

        var snapshot = await _uow.EmployeeSnapshots.GetByIdAsync(r.EmployeeId, ct)
            ?? throw new KeyNotFoundException($"Employee {r.EmployeeId} not found.");
        if (snapshot.EmploymentStatus == (int)EmploymentStatus.Terminated)
            throw new BusinessRuleException("HRM", "The employee is terminated.");

        var from = new DateTime(r.EffectiveFrom.Year, r.EffectiveFrom.Month, 1);
        var row = await _uow.Payroll.GetCompensationOnAsync(r.EmployeeId, from, ct);
        if (row == null)
        {
            row = new EmployeeCompensation { Id = Guid.NewGuid(), EmployeeId = r.EmployeeId, EffectiveFrom = from };
            _uow.Payroll.Add(row);
        }

        row.BaseSalary = r.BaseSalary;
        row.TaxableAllowance = r.TaxableAllowance;
        row.ExemptAllowance = r.ExemptAllowance;
        row.InsuranceSalary = r.InsuranceSalary;
        row.InsuranceEnabled = r.InsuranceEnabled;
        row.TaxCode = string.IsNullOrWhiteSpace(r.TaxCode) ? null : r.TaxCode.Trim();
        row.InsuranceNumber = string.IsNullOrWhiteSpace(r.InsuranceNumber) ? null : r.InsuranceNumber.Trim();
        row.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync(ct);
        return Map(row);
    }

    internal static CompensationDto Map(EmployeeCompensation c) =>
        new(c.Id, c.EmployeeId, c.EffectiveFrom, c.BaseSalary, c.TaxableAllowance, c.ExemptAllowance, c.InsuranceSalary, c.InsuranceEnabled, c.TaxCode, c.InsuranceNumber);
}

public class ListCompensationsQueryHandler : IRequestHandler<ListCompensationsQuery, List<CompensationDto>>
{
    private readonly IHrmUnitOfWork _uow;
    public ListCompensationsQueryHandler(IHrmUnitOfWork uow) => _uow = uow;

    public async Task<List<CompensationDto>> Handle(ListCompensationsQuery r, CancellationToken ct) =>
        (await _uow.Payroll.ListCompensationsAsync(r.EmployeeId, ct)).Select(SaveCompensationCommandHandler.Map).ToList();
}

public class ListSalaryProfilesQueryHandler : IRequestHandler<ListSalaryProfilesQuery, PagedResult<SalaryProfileDto>>
{
    private readonly IHrmUnitOfWork _uow;
    public ListSalaryProfilesQueryHandler(IHrmUnitOfWork uow) => _uow = uow;

    public async Task<PagedResult<SalaryProfileDto>> Handle(ListSalaryProfilesQuery r, CancellationToken ct)
    {
        var (items, total) = await _uow.EmployeeSnapshots.GetPagedAsync(r.Search, null, (int)EmploymentStatus.Active, r.Page, r.PageSize, ct);
        var ids = items.Select(i => i.Id).ToList();
        var current = await _uow.Payroll.GetCurrentCompensationsAsync(ids, DateTime.UtcNow, ct);

        var rows = new List<SalaryProfileDto>();
        foreach (var e in items)
        {
            var dependents = await _uow.Payroll.ListDependentsAsync(e.Id, ct);
            var now = DateTime.UtcNow;
            rows.Add(new SalaryProfileDto(
                e.Id, e.EmployeeNumber, e.FullName,
                dependents.Count(d => d.CoversPeriod(now.Year, now.Month)),
                current.TryGetValue(e.Id, out var c) ? SaveCompensationCommandHandler.Map(c) : null));
        }

        return new PagedResult<SalaryProfileDto>(rows, total, r.Page, r.PageSize);
    }
}

// ─── Dependents ───────────────────────────────────────────────────────────

public class AddDependentCommandHandler : IRequestHandler<AddDependentCommand, DependentDto>
{
    private readonly IHrmUnitOfWork _uow;
    public AddDependentCommandHandler(IHrmUnitOfWork uow) => _uow = uow;

    public async Task<DependentDto> Handle(AddDependentCommand r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.FullName)) throw new BusinessRuleException("HRM", "The dependent's name is required.");
        if (string.IsNullOrWhiteSpace(r.Relationship)) throw new BusinessRuleException("HRM", "The relationship is required.");
        var from = new DateTime(r.FromMonth.Year, r.FromMonth.Month, 1);
        DateTime? to = r.ToMonth.HasValue ? new DateTime(r.ToMonth.Value.Year, r.ToMonth.Value.Month, 1) : null;
        if (to.HasValue && to.Value < from) throw new BusinessRuleException("HRM", "The end month cannot be before the start month.");

        if (await _uow.EmployeeSnapshots.GetByIdAsync(r.EmployeeId, ct) == null)
            throw new KeyNotFoundException($"Employee {r.EmployeeId} not found.");

        var dependent = new Dependent
        {
            Id = Guid.NewGuid(),
            EmployeeId = r.EmployeeId,
            FullName = r.FullName.Trim(),
            Relationship = r.Relationship.Trim(),
            TaxCode = string.IsNullOrWhiteSpace(r.TaxCode) ? null : r.TaxCode.Trim(),
            FromMonth = from,
            ToMonth = to
        };
        _uow.Payroll.Add(dependent);
        await _uow.SaveChangesAsync(ct);
        return Map(dependent);
    }

    internal static DependentDto Map(Dependent d) => new(d.Id, d.EmployeeId, d.FullName, d.Relationship, d.TaxCode, d.FromMonth, d.ToMonth);
}

public class RemoveDependentCommandHandler : IRequestHandler<RemoveDependentCommand>
{
    private readonly IHrmUnitOfWork _uow;
    public RemoveDependentCommandHandler(IHrmUnitOfWork uow) => _uow = uow;

    public async Task Handle(RemoveDependentCommand r, CancellationToken ct)
    {
        var dependent = await _uow.Payroll.GetDependentAsync(r.DependentId, ct)
            ?? throw new KeyNotFoundException($"Dependent {r.DependentId} not found.");
        dependent.Remove();
        await _uow.SaveChangesAsync(ct);
    }
}

public class ListDependentsQueryHandler : IRequestHandler<ListDependentsQuery, List<DependentDto>>
{
    private readonly IHrmUnitOfWork _uow;
    public ListDependentsQueryHandler(IHrmUnitOfWork uow) => _uow = uow;

    public async Task<List<DependentDto>> Handle(ListDependentsQuery r, CancellationToken ct) =>
        (await _uow.Payroll.ListDependentsAsync(r.EmployeeId, ct)).Select(AddDependentCommandHandler.Map).ToList();
}

// ─── Rate sets ────────────────────────────────────────────────────────────

public class SaveRateSetCommandHandler : IRequestHandler<SaveRateSetCommand, PayrollRateSetDto>
{
    private readonly IHrmUnitOfWork _uow;
    public SaveRateSetCommandHandler(IHrmUnitOfWork uow) => _uow = uow;

    public async Task<PayrollRateSetDto> Handle(SaveRateSetCommand r, CancellationToken ct)
    {
        var rates = new[] { r.SocialEmployeeRate, r.HealthEmployeeRate, r.UnemploymentEmployeeRate, r.SocialEmployerRate, r.HealthEmployerRate, r.UnemploymentEmployerRate };
        if (rates.Any(x => x < 0 || x > 1)) throw new BusinessRuleException("HRM", "Insurance rates are fractions between 0 and 1 (8 % = 0.08).");
        if (r.PersonalDeduction < 0 || r.DependentDeduction < 0) throw new BusinessRuleException("HRM", "Deductions cannot be negative.");
        if (r.InsuranceSalaryCap <= 0 || r.UnemploymentSalaryCap <= 0) throw new BusinessRuleException("HRM", "The insurance salary caps must be greater than zero.");
        if (r.AnnualLeaveDays < 0) throw new BusinessRuleException("HRM", "Annual leave days cannot be negative.");
        if (string.IsNullOrWhiteSpace(r.Name)) throw new BusinessRuleException("HRM", "A name is required.");

        // The bracket table must be a proper progressive table: ascending limits, exactly one open-ended last bracket, rates 0..1.
        if (r.Brackets == null || r.Brackets.Count == 0) throw new BusinessRuleException("HRM", "At least one tax bracket is required.");
        var ordered = r.Brackets.OrderBy(b => b.UpTo ?? decimal.MaxValue).ToList();
        if (ordered.Count(b => b.UpTo == null) != 1 || ordered[^1].UpTo != null)
            throw new BusinessRuleException("HRM", "Exactly one tax bracket must have no upper limit, and it must be the last.");
        if (ordered.Any(b => b.Rate < 0 || b.Rate > 1) || ordered.Where(b => b.UpTo != null).Any(b => b.UpTo <= 0)
            || ordered.Where(b => b.UpTo != null).GroupBy(b => b.UpTo).Any(g => g.Count() > 1))
            throw new BusinessRuleException("HRM", "Tax brackets need distinct positive limits and rates between 0 and 1.");

        var from = new DateTime(r.EffectiveFrom.Year, r.EffectiveFrom.Month, 1);
        var all = await _uow.Payroll.ListRateSetsAsync(ct);
        if (all.Any(s => s.EffectiveFrom == from && s.Id != r.Id))
            throw new BusinessRuleException("HRM", $"A rate set already starts on {from:yyyy-MM}.");

        PayrollRateSet set;
        if (r.Id.HasValue)
        {
            set = await _uow.Payroll.GetRateSetByIdAsync(r.Id.Value, ct) ?? throw new KeyNotFoundException($"Rate set {r.Id} not found.");
            foreach (var b in set.Brackets.ToList()) _uow.Payroll.Remove(b);
            set.Brackets.Clear();
        }
        else
        {
            set = new PayrollRateSet { Id = Guid.NewGuid() };
            _uow.Payroll.Add(set);
        }

        set.EffectiveFrom = from;
        set.Name = r.Name.Trim();
        set.SocialEmployeeRate = r.SocialEmployeeRate;
        set.HealthEmployeeRate = r.HealthEmployeeRate;
        set.UnemploymentEmployeeRate = r.UnemploymentEmployeeRate;
        set.SocialEmployerRate = r.SocialEmployerRate;
        set.HealthEmployerRate = r.HealthEmployerRate;
        set.UnemploymentEmployerRate = r.UnemploymentEmployerRate;
        set.PersonalDeduction = r.PersonalDeduction;
        set.DependentDeduction = r.DependentDeduction;
        set.InsuranceSalaryCap = r.InsuranceSalaryCap;
        set.UnemploymentSalaryCap = r.UnemploymentSalaryCap;
        set.AnnualLeaveDays = r.AnnualLeaveDays;
        set.IsConfirmed = r.IsConfirmed;
        set.UpdatedAt = DateTime.UtcNow;
        foreach (var b in ordered)
        {
            var bracket = new PitBracket { Id = Guid.NewGuid(), RateSetId = set.Id, UpTo = b.UpTo, Rate = b.Rate };
            set.Brackets.Add(bracket);
            _uow.Payroll.Add(bracket);
        }

        await _uow.SaveChangesAsync(ct);
        return Map(set);
    }

    internal static PayrollRateSetDto Map(PayrollRateSet s) => new(
        s.Id, s.EffectiveFrom, s.Name, s.SocialEmployeeRate, s.HealthEmployeeRate, s.UnemploymentEmployeeRate,
        s.SocialEmployerRate, s.HealthEmployerRate, s.UnemploymentEmployerRate, s.PersonalDeduction, s.DependentDeduction,
        s.InsuranceSalaryCap, s.UnemploymentSalaryCap, s.AnnualLeaveDays, s.IsConfirmed,
        s.Brackets.Where(b => !b.IsDeleted).OrderBy(b => b.UpTo ?? decimal.MaxValue).Select(b => new PitBracketDto(b.UpTo, b.Rate)).ToList());
}

public class ListRateSetsQueryHandler : IRequestHandler<ListRateSetsQuery, List<PayrollRateSetDto>>
{
    private readonly IHrmUnitOfWork _uow;
    private readonly PayrollSupport _support;
    public ListRateSetsQueryHandler(IHrmUnitOfWork uow, PayrollSupport support) { _uow = uow; _support = support; }

    public async Task<List<PayrollRateSetDto>> Handle(ListRateSetsQuery r, CancellationToken ct)
    {
        // A tenant opening the settings page for the first time gets the default set to review.
        if (!await _uow.Payroll.AnyRateSetAsync(ct))
        {
            await _support.RateSetAsync(DateTime.UtcNow, ct);
            await _uow.SaveChangesAsync(ct);
        }
        return (await _uow.Payroll.ListRateSetsAsync(ct)).Select(SaveRateSetCommandHandler.Map).ToList();
    }
}

// ─── Leave balances ───────────────────────────────────────────────────────

public class ListLeaveBalancesQueryHandler : IRequestHandler<ListLeaveBalancesQuery, List<LeaveBalanceDto>>
{
    private readonly IHrmUnitOfWork _uow;
    private readonly PayrollSupport _support;
    public ListLeaveBalancesQueryHandler(IHrmUnitOfWork uow, PayrollSupport support) { _uow = uow; _support = support; }

    public async Task<List<LeaveBalanceDto>> Handle(ListLeaveBalancesQuery r, CancellationToken ct)
    {
        // Asking about one employee creates the annual balance of the year on first sight.
        if (r.EmployeeId.HasValue)
        {
            await _support.AnnualBalanceAsync(r.EmployeeId.Value, r.Year, ct);
            await _uow.SaveChangesAsync(ct);
        }
        return (await _uow.Payroll.ListLeaveBalancesAsync(r.EmployeeId, r.Year, ct))
            .Select(b => new LeaveBalanceDto(b.EmployeeId, b.Year, ((LeaveType)b.LeaveType).ToString(), b.TotalDays, b.UsedDays, b.RemainingDays))
            .ToList();
    }
}

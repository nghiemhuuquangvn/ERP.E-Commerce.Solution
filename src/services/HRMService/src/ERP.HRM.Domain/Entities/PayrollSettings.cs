using ERP.HRM.Domain.Payroll;
using ERP.Shared.Core.Domain;

namespace ERP.HRM.Domain.Entities;

/// <summary>
/// The insurance rates, tax deductions, caps and progressive tax table in force from <see cref="EffectiveFrom"/> (ROADMAP P1-7).
/// A change in the law is a new rate set with a later date; the pay slip keeps the id of the set it was calculated with.
/// <see cref="IsConfirmed"/> stays false for the seeded defaults until an accountant has reviewed them.
/// </summary>
public class PayrollRateSet : BaseEntity
{
    public DateTime EffectiveFrom { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal SocialEmployeeRate { get; set; }
    public decimal HealthEmployeeRate { get; set; }
    public decimal UnemploymentEmployeeRate { get; set; }
    public decimal SocialEmployerRate { get; set; }
    public decimal HealthEmployerRate { get; set; }
    public decimal UnemploymentEmployerRate { get; set; }
    public decimal PersonalDeduction { get; set; }
    public decimal DependentDeduction { get; set; }
    public decimal InsuranceSalaryCap { get; set; }
    public decimal UnemploymentSalaryCap { get; set; }
    public decimal AnnualLeaveDays { get; set; } = 12m;
    public bool IsConfirmed { get; set; }

    public List<PitBracket> Brackets { get; set; } = new();

    public PayrollRules ToRules() => new(
        Id, SocialEmployeeRate, HealthEmployeeRate, UnemploymentEmployeeRate,
        SocialEmployerRate, HealthEmployerRate, UnemploymentEmployerRate,
        PersonalDeduction, DependentDeduction, InsuranceSalaryCap, UnemploymentSalaryCap,
        Brackets.Where(b => !b.IsDeleted).OrderBy(b => b.UpTo ?? decimal.MaxValue).Select(b => new PitBracketRule(b.UpTo, b.Rate)).ToList());
}

public class PitBracket : BaseEntity
{
    public Guid RateSetId { get; set; }
    public decimal? UpTo { get; set; }
    public decimal Rate { get; set; }
}

/// <summary>
/// Salary terms of an employee from <see cref="EffectiveFrom"/>; the latest row on or before the end of the pay period applies.
/// HRM owns it (the master-data Employee holds no pay data).
/// </summary>
public class EmployeeCompensation : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal TaxableAllowance { get; set; }
    public decimal ExemptAllowance { get; set; }
    /// <summary>Salary insurance is paid on; null = the base salary.</summary>
    public decimal? InsuranceSalary { get; set; }
    public bool InsuranceEnabled { get; set; } = true;
    public string? TaxCode { get; set; }
    public string? InsuranceNumber { get; set; }
}

/// <summary>A person the employee supports, deducted from taxable income for every month from <see cref="FromMonth"/> to <see cref="ToMonth"/>.</summary>
public class Dependent : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public DateTime FromMonth { get; set; }
    public DateTime? ToMonth { get; set; }

    public void Remove()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool CoversPeriod(int year, int month)
    {
        var p = new DateTime(year, month, 1);
        return FromMonth.Date <= p && (ToMonth == null || ToMonth.Value.Date >= p);
    }
}

/// <summary>Days of one leave type an employee may take in a year, and how many are used.</summary>
public class LeaveBalance : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public int Year { get; set; }
    public int LeaveType { get; set; }
    public decimal TotalDays { get; set; }
    public decimal UsedDays { get; set; }

    public decimal RemainingDays => TotalDays - UsedDays;

    public void Consume(decimal days)
    {
        if (days > RemainingDays)
            throw new InvalidOperationException($"Only {RemainingDays:0.#} day(s) of leave are left; {days:0.#} requested.");
        UsedDays += days;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Release(decimal days)
    {
        UsedDays = Math.Max(0m, UsedDays - days);
        UpdatedAt = DateTime.UtcNow;
    }
}

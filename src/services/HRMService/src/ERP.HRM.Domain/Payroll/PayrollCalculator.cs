namespace ERP.HRM.Domain.Payroll;

/// <summary>One step of the progressive income-tax table: income up to <see cref="UpTo"/> (null = no upper limit) is taxed at <see cref="Rate"/>.</summary>
public record PitBracketRule(decimal? UpTo, decimal Rate);

/// <summary>The date-effective rules a pay slip is calculated with (a <c>PayrollRateSet</c> flattened for the calculator).</summary>
public record PayrollRules(
    Guid RateSetId,
    decimal SocialEmployeeRate,
    decimal HealthEmployeeRate,
    decimal UnemploymentEmployeeRate,
    decimal SocialEmployerRate,
    decimal HealthEmployerRate,
    decimal UnemploymentEmployerRate,
    decimal PersonalDeduction,
    decimal DependentDeduction,
    decimal InsuranceSalaryCap,
    decimal UnemploymentSalaryCap,
    IReadOnlyList<PitBracketRule> Brackets);

public record PayrollInput(
    decimal BaseSalary,
    decimal TaxableAllowance,
    decimal ExemptAllowance,
    decimal? InsuranceSalary,
    bool InsuranceEnabled,
    int DependentCount,
    decimal PaidDays,
    decimal StandardDays,
    decimal OvertimeHours);

public record PayrollResult(
    Guid RateSetId,
    decimal BaseSalary,
    decimal Allowances,
    decimal GrossSalary,
    decimal OvertimeHours,
    decimal OvertimePay,
    decimal PaidDays,
    decimal StandardDays,
    decimal EmployeeSocial,
    decimal EmployeeHealth,
    decimal EmployeeUnemployment,
    decimal EmployerSocial,
    decimal EmployerHealth,
    decimal EmployerUnemployment,
    decimal TaxableIncome,
    int DependentCount,
    decimal PersonalIncomeTax)
{
    public decimal EmployeeInsurance => EmployeeSocial + EmployeeHealth + EmployeeUnemployment;
    public decimal EmployerInsurance => EmployerSocial + EmployerHealth + EmployerUnemployment;

    /// <summary>What is withheld from the employee: insurance plus income tax (the pay slip's "Deductions").</summary>
    public decimal Deductions => EmployeeInsurance + PersonalIncomeTax;

    public decimal NetSalary => GrossSalary + OvertimePay - Deductions;
}

/// <summary>
/// Monthly payroll arithmetic (ROADMAP P1-7). Pure: the same input and rules always give the same pay slip. Amounts are whole
/// currency units (VND), rounded per line. Gross excludes overtime; net = gross + overtime - insurance - income tax.
/// </summary>
public static class PayrollCalculator
{
    public const decimal OvertimeMultiplier = 1.5m;
    public const decimal HoursPerDay = 8m;

    public static PayrollResult Calculate(PayrollInput input, PayrollRules rules)
    {
        if (input.StandardDays <= 0) throw new ArgumentException("Standard working days must be positive.", nameof(input));
        if (input.BaseSalary < 0) throw new ArgumentException("Base salary cannot be negative.", nameof(input));

        var paidDays = Math.Clamp(input.PaidDays, 0m, input.StandardDays);
        var proratedBase = Round(input.BaseSalary * paidDays / input.StandardDays);
        var allowances = input.TaxableAllowance + input.ExemptAllowance;
        var gross = proratedBase + allowances;

        var hourly = input.BaseSalary / (input.StandardDays * HoursPerDay);
        var overtimePay = Round(input.OvertimeHours * hourly * OvertimeMultiplier);

        // Insurance is paid on the contract's insurance salary (not the prorated pay), capped per scheme.
        decimal socialBase = 0m, unemploymentBase = 0m;
        if (input.InsuranceEnabled)
        {
            var insuranceSalary = input.InsuranceSalary ?? input.BaseSalary;
            socialBase = Math.Min(insuranceSalary, rules.InsuranceSalaryCap);
            unemploymentBase = Math.Min(insuranceSalary, rules.UnemploymentSalaryCap);
        }

        var employeeSocial = Round(socialBase * rules.SocialEmployeeRate);
        var employeeHealth = Round(socialBase * rules.HealthEmployeeRate);
        var employeeUnemployment = Round(unemploymentBase * rules.UnemploymentEmployeeRate);

        var taxable = Math.Max(0m,
            gross + overtimePay - input.ExemptAllowance
            - (employeeSocial + employeeHealth + employeeUnemployment)
            - rules.PersonalDeduction
            - input.DependentCount * rules.DependentDeduction);

        return new PayrollResult(
            rules.RateSetId, input.BaseSalary, allowances, gross, input.OvertimeHours, overtimePay, paidDays, input.StandardDays,
            employeeSocial, employeeHealth, employeeUnemployment,
            Round(socialBase * rules.SocialEmployerRate), Round(socialBase * rules.HealthEmployerRate), Round(unemploymentBase * rules.UnemploymentEmployerRate),
            taxable, input.DependentCount, Tax(taxable, rules.Brackets));
    }

    /// <summary>Progressive tax: each slice of income is taxed at its own bracket's rate.</summary>
    public static decimal Tax(decimal taxableIncome, IReadOnlyList<PitBracketRule> brackets)
    {
        if (taxableIncome <= 0) return 0m;
        decimal tax = 0m, lower = 0m;
        foreach (var b in brackets.OrderBy(x => x.UpTo ?? decimal.MaxValue))
        {
            var upper = b.UpTo ?? decimal.MaxValue;
            if (taxableIncome > lower) tax += (Math.Min(taxableIncome, upper) - lower) * b.Rate;
            if (taxableIncome <= upper) break;
            lower = upper;
        }
        return Round(tax);
    }

    /// <summary>Monday-Friday days of a month: the default number of paid days.</summary>
    public static int StandardWorkingDays(int year, int month)
    {
        var days = 0;
        for (var d = new DateTime(year, month, 1); d.Month == month; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days++;
        return days;
    }

    private static decimal Round(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);
}

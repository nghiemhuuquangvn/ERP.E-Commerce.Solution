using ERP.HRM.Application.Handlers;
using ERP.HRM.Domain.Payroll;
using FluentAssertions;
using Xunit;

namespace ERP.HRM.UnitTests.Payroll;

/// <summary>ROADMAP P1-7: insurance, progressive income tax with deductions, proration and overtime, checked by hand.</summary>
public class PayrollCalculatorTests
{
    private static PayrollRules Rules => DefaultPayrollRates.Create().ToRules();

    private static PayrollInput Input(
        decimal baseSalary = 30_000_000m, decimal taxable = 0m, decimal exempt = 0m, decimal? insuranceSalary = null,
        bool insurance = true, int dependents = 0, decimal paid = 22m, decimal standard = 22m, decimal overtime = 0m) =>
        new(baseSalary, taxable, exempt, insuranceSalary, insurance, dependents, paid, standard, overtime);

    [Fact]
    public void A_full_month_with_an_exempt_allowance_gives_the_hand_calculated_slip()
    {
        // base 30,000,000 + lunch allowance 1,000,000 (not taxed), no dependents.
        var r = PayrollCalculator.Calculate(Input(exempt: 1_000_000m), Rules);

        r.GrossSalary.Should().Be(31_000_000m);
        r.EmployeeSocial.Should().Be(2_400_000m);       // 8 %
        r.EmployeeHealth.Should().Be(450_000m);         // 1.5 %
        r.EmployeeUnemployment.Should().Be(300_000m);   // 1 %
        // 31,000,000 - 1,000,000 exempt - 3,150,000 insurance - 15,500,000 personal deduction
        r.TaxableIncome.Should().Be(11_350_000m);
        r.PersonalIncomeTax.Should().Be(635_000m);      // 10,000,000 x 5 % + 1,350,000 x 10 %
        r.Deductions.Should().Be(3_785_000m);
        r.NetSalary.Should().Be(27_215_000m);
        r.EmployerSocial.Should().Be(5_250_000m);       // 17.5 %
        r.EmployerHealth.Should().Be(900_000m);         // 3 %
        r.EmployerUnemployment.Should().Be(300_000m);   // 1 %
        r.EmployerInsurance.Should().Be(6_450_000m);
    }

    [Fact]
    public void Each_dependent_lowers_the_taxable_income_and_it_never_goes_below_zero()
    {
        var two = PayrollCalculator.Calculate(Input(dependents: 2), Rules);
        // 30,000,000 - 3,150,000 - 15,500,000 - 2 x 6,200,000 = -1,050,000 -> 0
        two.TaxableIncome.Should().Be(0m);
        two.PersonalIncomeTax.Should().Be(0m);

        var one = PayrollCalculator.Calculate(Input(dependents: 1), Rules);
        one.TaxableIncome.Should().Be(5_150_000m);
        one.PersonalIncomeTax.Should().Be(257_500m);    // 5 %
    }

    [Fact]
    public void Insurance_is_capped_per_scheme_and_can_be_switched_off()
    {
        var high = PayrollCalculator.Calculate(Input(baseSalary: 60_000_000m), Rules);
        high.EmployeeSocial.Should().Be(3_744_000m);        // 46,800,000 x 8 %
        high.EmployeeHealth.Should().Be(702_000m);          // 46,800,000 x 1.5 %
        high.EmployeeUnemployment.Should().Be(600_000m);    // 60,000,000 is under the unemployment cap

        var none = PayrollCalculator.Calculate(Input(insurance: false), Rules);
        none.EmployeeInsurance.Should().Be(0m);
        none.EmployerInsurance.Should().Be(0m);
    }

    [Fact]
    public void Insurance_is_paid_on_the_insurance_salary_when_one_is_set()
    {
        var r = PayrollCalculator.Calculate(Input(baseSalary: 30_000_000m, insuranceSalary: 10_000_000m), Rules);

        r.EmployeeSocial.Should().Be(800_000m);
        r.EmployerSocial.Should().Be(1_750_000m);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10_000_000, 500_000)]
    [InlineData(30_000_000, 2_500_000)]
    [InlineData(60_000_000, 8_500_000)]
    [InlineData(100_000_000, 20_500_000)]
    [InlineData(130_000_000, 31_000_000)]
    public void The_tax_table_is_progressive_at_every_bracket_boundary(decimal taxable, decimal tax) =>
        PayrollCalculator.Tax(taxable, Rules.Brackets).Should().Be(tax);

    [Fact]
    public void A_part_month_pays_the_base_in_proportion_but_insurance_stays_on_the_full_salary()
    {
        var r = PayrollCalculator.Calculate(Input(baseSalary: 22_000_000m, paid: 11m), Rules);

        r.GrossSalary.Should().Be(11_000_000m);
        r.EmployeeSocial.Should().Be(1_760_000m);       // 22,000,000 x 8 %
        r.PaidDays.Should().Be(11m);
    }

    [Fact]
    public void Overtime_is_paid_at_one_and_a_half_times_the_hourly_rate_and_is_taxable()
    {
        var r = PayrollCalculator.Calculate(Input(baseSalary: 22_000_000m, overtime: 10m), Rules);

        r.OvertimePay.Should().Be(1_875_000m);          // 22,000,000 / (22 x 8) = 125,000 x 10 x 1.5
        r.NetSalary.Should().Be(r.GrossSalary + r.OvertimePay - r.Deductions);
    }

    [Fact]
    public void Paid_days_are_limited_to_the_standard_days()
    {
        PayrollCalculator.Calculate(Input(paid: 30m), Rules).PaidDays.Should().Be(22m);
        PayrollCalculator.Calculate(Input(paid: -3m), Rules).GrossSalary.Should().Be(0m);
    }

    [Theory]
    [InlineData(2026, 10, 22)]
    [InlineData(2026, 2, 20)]
    [InlineData(2026, 11, 21)]
    public void Standard_days_are_the_weekdays_of_the_month(int year, int month, int days) =>
        PayrollCalculator.StandardWorkingDays(year, month).Should().Be(days);

    [Fact]
    public void The_default_rate_set_is_a_draft_with_five_brackets_and_one_open_end()
    {
        var set = DefaultPayrollRates.Create();

        set.IsConfirmed.Should().BeFalse();
        set.Brackets.Should().HaveCount(5);
        set.Brackets.Count(b => b.UpTo == null).Should().Be(1);
    }
}

using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-2l: the day count of a loan (schedule / actual-365 / actual-360) for accrual and for the default interest paid.</summary>
public class LoanDayCountTests
{
    private static readonly DateTime Start = new(2026, 11, 1);

    // 1,000,000 at 12 % from 1 Nov 2026, one installment due 1 Dec (30 days)
    private static Loan OneInstallment(LoanDayCount basis, decimal scheduledInterest = 0m) =>
        Loan.Create("LN-D", "ACB", 1_000_000m, 12m, Start, new[] { (new DateTime(2026, 12, 1), 1_000_000m, scheduledInterest) }, null, null, (int)basis);

    // two installments of 500,000: 1 Dec 2026 and 1 Jan 2027
    private static Loan TwoInstallments(LoanDayCount basis) =>
        Loan.Create("LN-D2", "ACB", 1_000_000m, 12m, Start,
            new[] { (new DateTime(2026, 12, 1), 500_000m, 0m), (new DateTime(2027, 1, 1), 500_000m, 0m) }, null, null, (int)basis);

    [Fact]
    public void Actual_360_accrues_principal_times_rate_over_360_for_each_day_even_when_the_schedule_has_no_interest()
    {
        var loan = OneInstallment(LoanDayCount.Actual360);

        loan.AccrueInterest(new DateTime(2026, 11, 16)).Should().ContainSingle().Which.Amount.Should().Be(5000.00m);     // 15 days
        loan.AccrueInterest(new DateTime(2026, 12, 1)).Should().ContainSingle().Which.Amount.Should().Be(5000.00m);      // 15 more = 10,000 in total
        loan.Installments.Single().AccruedInterest.Should().Be(10_000.00m);
    }

    [Fact]
    public void Actual_365_gives_less_than_actual_360_for_the_same_days()
    {
        var loan = OneInstallment(LoanDayCount.Actual365);

        loan.AccrueInterest(new DateTime(2026, 12, 1)).Should().ContainSingle().Which.Amount.Should().Be(9863.01m);
    }

    [Fact]
    public void A_leap_year_period_has_its_real_number_of_days()
    {
        var loan = Loan.Create("LN-L", "ACB", 1_000_000m, 12m, new DateTime(2028, 2, 1),
            new[] { (new DateTime(2028, 3, 1), 1_000_000m, 0m) }, null, null, (int)LoanDayCount.Actual360);

        loan.AccrueInterest(new DateTime(2028, 3, 1)).Should().ContainSingle().Which.Amount.Should().Be(9666.67m);       // 29 days
    }

    [Fact]
    public void Running_it_twice_for_the_same_date_posts_nothing_more()
    {
        var loan = OneInstallment(LoanDayCount.Actual360);
        loan.AccrueInterest(new DateTime(2026, 11, 16));

        loan.AccrueInterest(new DateTime(2026, 11, 16)).Should().BeEmpty();
        loan.AccrueInterest(new DateTime(2026, 11, 10)).Should().BeEmpty();
    }

    [Fact]
    public void Repaying_an_earlier_installment_lowers_the_base_of_the_next_period()
    {
        var loan = TwoInstallments(LoanDayCount.Actual360);
        var first = loan.Installments.OrderBy(i => i.DueDate).First();
        loan.RecordRepayment(first.Id, new DateTime(2026, 12, 1));       // interest by days: 1,000,000 × 12 % × 30 / 360

        first.InterestAmount.Should().Be(10_000.00m);
        var accrual = loan.AccrueInterest(new DateTime(2027, 1, 1));      // 31 days on 500,000
        accrual.Should().ContainSingle().Which.Amount.Should().Be(5166.67m);
    }

    [Fact]
    public void The_default_interest_paid_follows_the_payment_date_and_an_explicit_figure_wins()
    {
        var early = OneInstallment(LoanDayCount.Actual360);
        early.RecordRepayment(early.Installments.Single().Id, new DateTime(2026, 11, 21));    // 20 days
        early.Installments.Single().InterestAmount.Should().Be(6666.67m);

        var explicitFigure = OneInstallment(LoanDayCount.Actual360);
        explicitFigure.RecordRepayment(explicitFigure.Installments.Single().Id, new DateTime(2026, 11, 21), 7000m);
        explicitFigure.Installments.Single().InterestAmount.Should().Be(7000m);
    }

    [Fact]
    public void A_scheduled_loan_keeps_the_original_rule_and_the_scheduled_interest_as_default()
    {
        var loan = OneInstallment(LoanDayCount.Schedule, scheduledInterest: 90m);

        loan.AccrueInterest(new DateTime(2026, 11, 16)).Should().ContainSingle().Which.Amount.Should().Be(45m);
        loan.RecordRepayment(loan.Installments.Single().Id, new DateTime(2026, 11, 21));
        loan.Installments.Single().InterestAmount.Should().Be(90m);
    }

    [Fact]
    public void Interest_due_today_of_a_scheduled_loan_is_a_share_of_the_period_and_zero_before_it_starts()
    {
        var loan = OneInstallment(LoanDayCount.Schedule, scheduledInterest: 90m);

        loan.InterestToDate(loan.Installments.Single(), new DateTime(2026, 11, 16)).Should().Be(45m);
        loan.InterestToDate(loan.Installments.Single(), new DateTime(2026, 10, 1)).Should().Be(0m);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    public void An_unknown_day_count_is_refused(int basis)
    {
        var act = () => Loan.Create("LN-X", "ACB", 1000m, 12m, Start, new[] { (new DateTime(2026, 12, 1), 1000m, 0m) }, null, null, basis);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Counting_by_days_needs_an_interest_rate()
    {
        var act = () => Loan.Create("LN-X", "ACB", 1000m, 0m, Start, new[] { (new DateTime(2026, 12, 1), 1000m, 0m) }, null, null, (int)LoanDayCount.Actual360);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void The_day_count_cannot_change_after_interest_was_accrued()
    {
        var loan = OneInstallment(LoanDayCount.Schedule, scheduledInterest: 90m);
        loan.AccrueInterest(new DateTime(2026, 11, 16));

        var act = () => loan.SetDayCountBasis((int)LoanDayCount.Actual360);

        act.Should().Throw<InvalidOperationException>();
    }
}

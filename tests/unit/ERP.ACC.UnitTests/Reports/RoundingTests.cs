using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-2e: statements in thousand / million VND — rows rounded per account group, totals added from the rounded rows, checks on exact amounts.</summary>
public class RoundingTests
{
    private readonly List<Account> _chart = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();

    private Account A(string number) => _chart.Single(a => a.AccountNumber == number);

    private Dictionary<Guid, AccountBalanceSummary> Balances(params (string Number, decimal Debit, decimal Credit)[] lines) =>
        lines.ToDictionary(l => A(l.Number).Id, l => new AccountBalanceSummary { AccountId = A(l.Number).Id, TotalDebit = l.Debit, TotalCredit = l.Credit });

    private static decimal Row(FinancialStatementDto s, string code) => s.Rows.Single(r => r.Code == code).Amount;

    [Theory]
    [InlineData(1499, 1)]
    [InlineData(1500, 2)]    // half away from zero
    [InlineData(-1500, -2)]
    [InlineData(-1499, -1)]
    [InlineData(499, 0)]
    [InlineData(0, 0)]
    public void Amounts_round_half_away_from_zero_in_thousands(int vnd, int expected) =>
        StatementEngine.RoundTo(vnd, 1000m).Should().Be(expected);

    [Fact]
    public void Million_rounds_to_millions_and_vnd_is_left_alone()
    {
        StatementEngine.RoundTo(2_500_000m, 1_000_000m).Should().Be(3m);
        StatementEngine.RoundTo(1_234.56m, 1m).Should().Be(1_234.56m);
    }

    [Fact]
    public void The_income_statement_rounds_each_row_and_adds_the_rounded_rows()
    {
        // revenue 1,600 and 1,600 → exact total 3,200; but cost 1,400 → exact profit 1,800
        var balances = Balances(("5111", 0m, 1600m), ("5113", 0m, 1600m), ("632", 1400m, 0m));
        // 5111 / 5113 are sub-accounts of row 01 (511): the row takes the sum, so rounding happens on the row (3,200 → 3), not per account
        var s = StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.IncomeStatement)!, _chart, balances, null, 1000m);

        Row(s, "01").Should().Be(3m);
        Row(s, "11").Should().Be(1m);       // 1,400 → 1
        Row(s, "20").Should().Be(2m);       // 3 − 1 from the rounded rows (the exact gross profit 1,800 would round to 2 as well)
        Row(s, "60").Should().Be(2m);
        s.Unit.Should().Be("thousand VND");
        s.UnitDivisor.Should().Be(1000m);
    }

    [Fact]
    public void A_total_is_the_sum_of_the_rounded_rows_even_when_that_differs_from_the_rounded_exact_total()
    {
        // revenue 1,400 and expense 1,400 + 100 financial cost: exact profit −100 → rounds to 0, but rows 01 − 22 = 1 − 1 = 0; use values that expose the difference
        var balances = Balances(("5111", 0m, 1400m), ("635", 600m, 0m), ("642", 600m, 0m));
        var s = StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.IncomeStatement)!, _chart, balances, null, 1000m);

        Row(s, "22").Should().Be(1m);   // 600 → 1
        Row(s, "26").Should().Be(1m);   // 600 → 1
        Row(s, "01").Should().Be(1m);   // 1,400 → 1
        Row(s, "60").Should().Be(-1m);  // 1 − 1 − 1; the exact profit 200 would round to 0
    }

    [Fact]
    public void The_balance_check_uses_exact_amounts_and_a_rounding_difference_is_reported()
    {
        // cash 1,400 (row 110 → 1) and inventory 1,400 (row 140 → 1) = 2 rounded, exact 2,800; equity 2,800 → 3: rounded totals differ by one
        var balances = Balances(("1111", 1400m, 0m), ("1561", 1400m, 0m), ("4111", 0m, 2800m));
        var balanced = StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.BalanceSheet)!, _chart, balances, null, 1000m);

        balanced.IsBalanced.Should().BeTrue(); // exact 270 = 440 = 2,800
        balanced.Rows.Single(r => r.Code == "270").Amount.Should().NotBe(balanced.Rows.Single(r => r.Code == "440").Amount);
        balanced.Notes.Should().Contain(n => n.StartsWith("Rounding difference of"));

        var exact = StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.BalanceSheet)!, _chart, balances, null);
        exact.IsBalanced.Should().BeTrue();
        exact.Notes.Should().NotContain(n => n.StartsWith("Rounding difference"));
        exact.Unit.Should().Be("VND");
    }

    [Fact]
    public void A_genuinely_unbalanced_sheet_stays_unbalanced_after_rounding()
    {
        var balances = Balances(("1111", 1000m, 0m), ("4111", 0m, 400m));
        var s = StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.BalanceSheet)!, _chart, balances, null, 1_000m);

        s.IsBalanced.Should().BeFalse();
    }

    [Fact]
    public void The_prior_column_is_rounded_the_same_way()
    {
        var s = StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.IncomeStatement)!, _chart,
            Balances(("5111", 0m, 2600m)), Balances(("5111", 0m, 1499m)), 1000m);

        s.Rows.Single(r => r.Code == "01").Amount.Should().Be(3m);
        s.Rows.Single(r => r.Code == "01").PriorAmount.Should().Be(1m);
    }

    [Fact]
    public void Cash_flow_rows_are_rounded_and_the_totals_follow_the_rounded_rows()
    {
        var movements = new[] { new CashMovement(A("5111"), 1600m), new CashMovement(A("331"), -1400m) };
        var s = CashFlowStatementEngine.Evaluate(CashFlowLayouts.Layout, new CashFlowColumn(movements, 2600m, 2800m), null, 1000m);

        Row(s, "01").Should().Be(2m);
        Row(s, "02").Should().Be(-1m);
        Row(s, "20").Should().Be(1m);
        Row(s, "60").Should().Be(3m);    // 2,600 → 3
        Row(s, "70").Should().Be(4m);    // 1 + 3
        s.IsBalanced.Should().BeTrue();  // exact: 2,600 + 200 = 2,800
        s.Notes.Should().Contain(n => n.StartsWith("Rounding difference")); // 4 vs the rounded 2,800 = 3
    }

    // ---- handler: the unit comes from the request, else the profile, else VND; the header only when the profile has text

    private static (GetIncomeStatementQueryHandler Handler, List<Account> Chart) Handler(ReportProfile? profile)
    {
        var chart = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => chart.ToList());
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(l => l.GetActivityBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new List<AccountBalanceSummary> { new() { AccountId = chart.Single(a => a.AccountNumber == "5111").Id, TotalCredit = 2600m } });
        var profiles = new Mock<IReportProfileRepository>();
        profiles.Setup(p => p.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        return (new GetIncomeStatementQueryHandler(accounts.Object, lines.Object, profiles.Object, new Mock<IStatementMappingRepository>().Object), chart);
    }

    private static ReportProfile Profile(AmountUnit unit, string name = "ACME")
    {
        var p = ReportProfile.Create(Guid.NewGuid());
        p.Set(name, "0312345678", "1 Lê Lợi", "A", "B", "C", unit);
        return p;
    }

    private static GetIncomeStatementQuery Query(string? unit = null) =>
        new() { FromDate = new DateTime(2026, 10, 1), ToDate = new DateTime(2026, 10, 31), Unit = unit };

    [Fact]
    public async Task Without_a_profile_the_statement_is_plain_vnd_with_no_header()
    {
        var s = await Handler(null).Handler.Handle(Query(), default);

        s.Unit.Should().Be("VND");
        Row(s, "01").Should().Be(2600m);
        s.Header.Should().BeNull();
    }

    [Fact]
    public async Task The_profile_unit_and_header_are_used_and_a_request_can_override_the_unit()
    {
        var handler = Handler(Profile(AmountUnit.Thousand)).Handler;

        var fromProfile = await handler.Handle(Query(), default);
        fromProfile.Unit.Should().Be("thousand VND");
        Row(fromProfile, "01").Should().Be(3m);
        fromProfile.Header!.CompanyName.Should().Be("ACME");
        fromProfile.Header.ChiefAccountant.Should().Be("B");

        var overridden = await handler.Handle(Query("vnd"), default);
        overridden.Unit.Should().Be("VND");
        Row(overridden, "01").Should().Be(2600m);
        overridden.Header.Should().NotBeNull(); // the header stays

        (await handler.Handle(Query("Million"), default)).UnitDivisor.Should().Be(1_000_000m);
    }

    [Fact]
    public async Task An_unknown_unit_in_the_request_is_refused()
    {
        await FluentActions.Awaiting(() => Handler(null).Handler.Handle(Query("dong"), default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*Vnd, Thousand or Million*");
    }

    [Fact]
    public void A_profile_with_no_text_prints_no_header()
    {
        var empty = ReportProfile.Create(Guid.NewGuid());
        empty.Set("", "", "", "", "", "", AmountUnit.Million);

        ReportProfileMapper.Header(empty).Should().BeNull();
        ReportProfileMapper.ResolveUnit(null, empty).Should().Be(AmountUnit.Million);
    }
}

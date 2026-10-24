using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-2c: the B01 / B02 layouts, the engine that evaluates them, and the guarantee that no account is silently left out.</summary>
public class StatementTests
{
    private static readonly List<Account> Chart = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();

    private static Account A(string number) => Chart.Single(a => a.AccountNumber == number);

    private static Dictionary<Guid, AccountBalanceSummary> Balances(params (string Account, decimal Debit, decimal Credit)[] lines) =>
        lines.GroupBy(l => A(l.Account).Id).ToDictionary(g => g.Key,
            g => new AccountBalanceSummary { AccountId = g.Key, TotalDebit = g.Sum(x => x.Debit), TotalCredit = g.Sum(x => x.Credit) });

    private static decimal Row(FinancialStatementDto s, string code) => s.Rows.Single(r => r.Code == code).Amount;

    private static FinancialStatementDto B01(Dictionary<Guid, AccountBalanceSummary> current, Dictionary<Guid, AccountBalanceSummary>? prior = null) =>
        StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.BalanceSheet)!, Chart, current, prior);

    private static FinancialStatementDto B02(Dictionary<Guid, AccountBalanceSummary> current, Dictionary<Guid, AccountBalanceSummary>? prior = null) =>
        StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.IncomeStatement)!, Chart, current, prior);

    // ---- layout integrity ----

    [Fact]
    public void Row_codes_are_unique_and_every_formula_points_at_an_existing_row_without_cycles()
    {
        foreach (var layout in StatementLayouts.All)
        {
            layout.Rows.Select(r => r.Code).Should().OnlyHaveUniqueItems($"{layout.Id} row codes");
            var codes = layout.Rows.Select(r => r.Code).ToHashSet();
            layout.Rows.SelectMany(r => r.Formula).Select(f => f.Code).Should().OnlyContain(c => codes.Contains(c), $"{layout.Id} formulas");

            // evaluating with no data walks every formula; a cycle would throw
            var act = () => StatementEngine.Evaluate(layout, Chart, new Dictionary<Guid, AccountBalanceSummary>(), null);
            act.Should().NotThrow();
        }
    }

    [Fact]
    public void No_account_is_taken_by_two_terms_of_the_same_statement_on_the_same_side()
    {
        foreach (var layout in StatementLayouts.All)
        {
            var terms = layout.Rows.SelectMany(r => r.Terms).ToList();
            foreach (var account in Chart)
                foreach (var balance in new[] { 1m, -1m })
                {
                    var matches = terms.Where(t => account.AccountNumber.StartsWith(t.Prefix, StringComparison.Ordinal)
                        && (t.Side == BalanceSide.Net || (t.Side == BalanceSide.DebitOnly) == (balance > 0))).ToList();
                    matches.Count.Should().BeLessThanOrEqualTo(1,
                        $"{layout.Id}: account {account.AccountNumber} ({(balance > 0 ? "debit" : "credit")} balance) is taken by {string.Join(", ", matches.Select(m => m.Prefix))}");
                }
        }
    }

    // Accounts the two statements deliberately do not take: production costs flow into inventory / cost of goods sold, 911 is the temporary closing account.
    private static readonly string[] Excluded = { "621", "622", "627", "911" };

    [Fact]
    public void Every_account_of_the_vietnamese_template_is_on_a_statement_except_the_documented_exclusions()
    {
        var parentNumbers = Chart.Where(a => a.ParentAccountId.HasValue).Select(a => Chart.Single(p => p.Id == a.ParentAccountId).AccountNumber).ToHashSet();
        var statementTypes = new Dictionary<int, StatementLayout>();
        foreach (var layout in StatementLayouts.All)
            foreach (var type in layout.AccountTypes)
                statementTypes[(int)type] = layout;

        foreach (var account in Chart.Where(a => !parentNumbers.Contains(a.AccountNumber)))
        {
            if (Excluded.Contains(account.AccountNumber))
                continue;
            var layout = statementTypes[account.AccountType];
            var prefixes = layout.Rows.SelectMany(r => r.Terms).ToList();
            foreach (var net in new[] { 1m, -1m })
            {
                var covered = prefixes.Any(t => account.AccountNumber.StartsWith(t.Prefix, StringComparison.Ordinal)
                    && (t.Side == BalanceSide.Net || (t.Side == BalanceSide.DebitOnly) == (net > 0)));
                covered.Should().BeTrue($"{account.AccountNumber} {account.Name} ({(net > 0 ? "debit" : "credit")} balance) must be on {layout.Id} or listed as excluded");
            }
        }
    }

    // ---- B02 ----

    [Fact]
    public void B02_builds_net_revenue_gross_profit_and_the_result_from_the_accounts()
    {
        var s = B02(Balances(
            ("5111", 0m, 1000m), ("5212", 100m, 0m),          // revenue 1000, returned goods 100
            ("632", 400m, 0m), ("515", 0m, 20m), ("635", 30m, 0m), ("6421", 120m, 0m), ("641", 50m, 0m),
            ("711", 0m, 40m), ("811", 10m, 0m), ("8211", 60m, 0m)));

        Row(s, "01").Should().Be(1000m);
        Row(s, "02").Should().Be(100m);
        Row(s, "10").Should().Be(900m);
        Row(s, "20").Should().Be(500m);                         // 900 - 400
        Row(s, "30").Should().Be(500m + 20m - 30m - 50m - 120m); // 320
        Row(s, "40").Should().Be(30m);
        Row(s, "50").Should().Be(350m);
        Row(s, "60").Should().Be(290m);
        s.Unmapped.Should().BeEmpty();
        s.IsBalanced.Should().BeNull();
    }

    [Fact]
    public void B02_has_a_prior_period_column_and_reports_a_production_cost_account_as_unmapped()
    {
        var s = B02(
            Balances(("5111", 0m, 1000m), ("627", 70m, 0m)),
            Balances(("5111", 0m, 800m)));

        Row(s, "01").Should().Be(1000m);
        s.Rows.Single(r => r.Code == "01").PriorAmount.Should().Be(800m);
        s.Rows.Single(r => r.Code == "60").PriorAmount.Should().Be(800m);
        s.Unmapped.Should().ContainSingle().Which.AccountNumber.Should().Be("627");
        s.Notes.Should().Contain(n => n.Contains("not part of this statement"));
    }

    // ---- B01 ----

    [Fact]
    public void B01_balances_assets_against_liabilities_and_equity_and_signs_contra_accounts()
    {
        var s = B01(Balances(
            ("1121", 5000m, 0m),                                   // bank
            ("131", 800m, 0m),                                     // customers owe 800
            ("1561", 1200m, 0m),                                   // stock
            ("211", 3000m, 0m), ("2141", 0m, 500m),                // equipment, accumulated depreciation (contra)
            ("2294", 0m, 100m),                                    // stock provision (contra)
            ("331", 0m, 700m), ("3331", 0m, 150m), ("334", 0m, 250m),
            ("341", 0m, 2000m),
            ("4111", 0m, 6000m),
            ("4212", 0m, 300m)));

        Row(s, "110").Should().Be(5000m);
        Row(s, "130").Should().Be(800m);
        Row(s, "140").Should().Be(1200m - 100m);
        Row(s, "220").Should().Be(3000m - 500m);
        Row(s, "270").Should().Be(5000m + 800m + 1100m + 2500m);   // 9400
        Row(s, "311").Should().Be(700m);
        Row(s, "313").Should().Be(150m);
        Row(s, "320").Should().Be(2000m);
        Row(s, "300").Should().Be(700m + 150m + 250m + 2000m);       // 3100
        Row(s, "410").Should().Be(6300m);
        Row(s, "440").Should().Be(9400m);
        s.IsBalanced.Should().BeTrue();
        s.Unmapped.Should().BeEmpty();
    }

    [Fact]
    public void B01_splits_receivables_and_payables_by_the_side_of_their_balance()
    {
        var s = B01(Balances(
            ("131", 0m, 300m),            // a customer paid in advance: a liability
            ("331", 200m, 0m),            // advance to a supplier: an asset
            ("1111", 100m, 0m), ("4111", 0m, 100m), ("5111", 0m, 0m)));

        Row(s, "131").Should().Be(0m);
        Row(s, "312").Should().Be(300m);
        Row(s, "132").Should().Be(200m);
        Row(s, "311").Should().Be(0m);
        Row(s, "270").Should().Be(300m);              // 100 cash + 200 advance
        Row(s, "440").Should().Be(400m);              // 300 customers' advances + 100 equity ... plus nothing else
        s.IsBalanced.Should().BeFalse("the entries above are not a balanced book: 100 + 200 assets vs 300 + 100 sources");
    }

    [Fact]
    public void B01_adds_the_result_that_no_closing_entry_has_moved_yet_so_it_balances_before_closing()
    {
        // capital 1000 paid in, sold goods for 600 in cash, paid 100 of expenses: no closing entry yet
        var book = Balances(("1111", 1500m, 0m), ("4111", 0m, 1000m), ("5111", 0m, 600m), ("6421", 100m, 0m));

        var s = B01(book);

        Row(s, "421b").Should().Be(500m);
        Row(s, "270").Should().Be(1500m);
        Row(s, "440").Should().Be(1500m);
        s.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void B01_after_closing_shows_the_same_totals_with_the_result_in_retained_earnings()
    {
        var closed = Balances(("1111", 1500m, 0m), ("4111", 0m, 1000m), ("5111", 600m, 600m), ("6421", 100m, 100m), ("4212", 0m, 500m));

        var s = B01(closed);

        Row(s, "421b").Should().Be(500m);   // from account 4212, nothing unclosed left
        s.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void B01_prior_column_uses_the_balances_of_the_comparison_date_and_flags_imbalance_in_either_column()
    {
        var current = Balances(("1111", 1500m, 0m), ("4111", 0m, 1500m));
        var prior = Balances(("1111", 1000m, 0m), ("4111", 0m, 900m));

        var s = B01(current, prior);

        s.Rows.Single(r => r.Code == "270").PriorAmount.Should().Be(1000m);
        s.IsBalanced.Should().BeFalse();
    }

    [Fact]
    public void A_simple_chart_tenant_sees_every_account_as_unmapped()
    {
        var simple = ChartTemplates.Find(ChartTemplates.Simple14)!.BuildAccounts();
        var cash = simple.Single(a => a.AccountNumber == "1000");
        var balances = new Dictionary<Guid, AccountBalanceSummary> { [cash.Id] = new() { AccountId = cash.Id, TotalDebit = 500m } };

        var s = StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.BalanceSheet)!, simple, balances, null);

        s.Unmapped.Should().ContainSingle().Which.AccountNumber.Should().Be("1000");
        Row(s, "270").Should().Be(0m);
    }
}

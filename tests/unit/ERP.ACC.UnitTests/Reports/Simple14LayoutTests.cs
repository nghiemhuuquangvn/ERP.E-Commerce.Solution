using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-2i: B01 / B02 / B03 for the platform's 14-account chart, chosen by the accounts the tenant has.</summary>
public class Simple14LayoutTests
{
    private readonly List<Account> _s14 = ChartTemplates.Find(ChartTemplates.Simple14)!.BuildAccounts();
    private readonly List<Account> _vas = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();

    private Account A(string number) => _s14.Single(a => a.AccountNumber == number);

    // ---- which layout ----

    [Fact]
    public void The_chart_is_detected_from_the_accounts()
    {
        ChartTemplates.Detect(_s14).Should().Be("Simple14");
        ChartTemplates.Detect(_vas).Should().Be("Vas99");
        ChartTemplates.Detect(new List<Account>()).Should().Be("Vas99");            // no accounts: the old behaviour
        ChartTemplates.Detect(_s14.Concat(_vas)).Should().Be("Vas99");               // a tenant that applied the Vietnamese chart on top
    }

    [Fact]
    public void Each_statement_has_a_layout_per_chart()
    {
        StatementLayouts.For("B01-DN", _s14)!.ChartTemplate.Should().Be("Simple14");
        StatementLayouts.For("B02-DN", _s14)!.ChartTemplate.Should().Be("Simple14");
        StatementLayouts.For("B01-DN", _vas)!.ChartTemplate.Should().Be("Vas99");
        CashFlowLayouts.For("Simple14").ChartTemplate.Should().Be("Simple14");
        CashFlowLayouts.For("Vas99").Should().BeSameAs(CashFlowLayouts.Layout);
        IndirectCashFlowLayouts.For("Simple14").ChartTemplate.Should().Be("Simple14");
    }

    [Fact]
    public void Every_account_of_the_14_account_chart_is_on_the_balance_sheet_or_the_income_statement()
    {
        var terms = StatementLayouts.Simple14.SelectMany(l => l.Rows).SelectMany(r => r.Terms).Select(t => t.Prefix).ToList();

        foreach (var account in _s14)
            terms.Any(p => account.AccountNumber.StartsWith(p, StringComparison.Ordinal)).Should().BeTrue($"account {account.AccountNumber} must be on a statement");
    }

    [Fact]
    public void The_layouts_are_consistent()
    {
        foreach (var layout in StatementLayouts.Simple14)
        {
            var codes = layout.Rows.Select(r => r.Code).ToList();
            codes.Should().OnlyHaveUniqueItems($"{layout.Id} Simple14");
            foreach (var part in layout.Rows.SelectMany(r => r.Formula))
                codes.Should().Contain(part.Code);
        }
        foreach (var layout in new[] { CashFlowLayouts.Simple14Layout })
        {
            var codes = layout.Rows.Select(r => r.Code).ToList();
            codes.Should().OnlyHaveUniqueItems();
            foreach (var part in layout.Rows.SelectMany(r => r.Formula))
                codes.Should().Contain(part.Code);
        }
        var indirect = IndirectCashFlowLayouts.Simple14Layout.Rows.Select(r => r.Code).ToList();
        indirect.Should().OnlyHaveUniqueItems();
        foreach (var part in IndirectCashFlowLayouts.Simple14Layout.Rows.SelectMany(r => r.Formula))
            indirect.Should().Contain(part.Code);
    }

    // ---- B01 / B02 ----

    private Dictionary<Guid, AccountBalanceSummary> Balances(params (string Number, decimal Debit, decimal Credit)[] lines) =>
        lines.ToDictionary(l => A(l.Number).Id, l => new AccountBalanceSummary { AccountId = A(l.Number).Id, TotalDebit = l.Debit, TotalCredit = l.Credit });

    private static decimal Row(FinancialStatementDto s, string code) => s.Rows.Single(r => r.Code == code).Amount;

    [Fact]
    public void A_14_account_ledger_fills_the_balance_sheet_and_balances_with_nothing_unmapped()
    {
        // cash 1,500, receivable 400, stock 300, equipment 500 (assets 2,700)
        // payables 600, accrued 100, tax 50; revenue 1,000 − cost 300 − expenses 250 = profit 450 not yet closed
        var balances = Balances(
            ("1000", 1500m, 0m), ("1100", 400m, 0m), ("1200", 300m, 0m), ("1500", 500m, 0m),
            ("2000", 0m, 600m), ("2100", 0m, 100m), ("2200", 0m, 50m), ("3000", 0m, 1500m),
            ("4000", 0m, 1000m), ("5000", 300m, 0m), ("5100", 150m, 0m), ("5200", 100m, 0m));
        // a double-entry ledger balances: debits 2,700 + 550 expenses = credits 750 + 1,500 capital + 1,000 revenue = 3,250
        var s = StatementEngine.Evaluate(StatementLayouts.For("B01-DN", _s14)!, _s14, balances, null);

        Row(s, "110").Should().Be(1500m);
        Row(s, "131").Should().Be(400m);
        Row(s, "141").Should().Be(300m);
        Row(s, "100").Should().Be(2200m);
        Row(s, "220").Should().Be(500m);
        Row(s, "270").Should().Be(2700m);
        Row(s, "311").Should().Be(600m);
        Row(s, "313").Should().Be(50m);
        Row(s, "319").Should().Be(100m);
        Row(s, "300").Should().Be(750m);
        Row(s, "411").Should().Be(1500m);
        Row(s, "421b").Should().Be(450m);   // the result not yet closed
        Row(s, "440").Should().Be(2700m);
        s.IsBalanced.Should().BeTrue();
        s.Unmapped.Should().BeEmpty();
    }

    [Fact]
    public void The_income_statement_reads_revenue_cost_and_expenses()
    {
        var s = StatementEngine.Evaluate(StatementLayouts.For("B02-DN", _s14)!, _s14,
            Balances(("4000", 0m, 900m), ("4100", 0m, 100m), ("5000", 300m, 0m), ("5100", 150m, 0m), ("5200", 100m, 0m)), null);

        Row(s, "01").Should().Be(1000m);
        Row(s, "11").Should().Be(300m);
        Row(s, "20").Should().Be(700m);
        Row(s, "26").Should().Be(250m);
        Row(s, "30").Should().Be(450m);
        Row(s, "60").Should().Be(450m);
        s.Unmapped.Should().BeEmpty();
    }

    [Fact]
    public void The_maturity_split_works_on_the_accounts_of_the_14_account_chart()
    {
        var balances = Balances(("1100", 1000m, 0m), ("2000", 0m, 800m), ("1000", 0m, 0m), ("3000", 0m, 200m));
        var s = StatementEngine.Evaluate(StatementLayouts.For("B01-DN", _s14)!, _s14, balances, null, 1m, null, new StatementMaturity(300m, 200m));

        Row(s, "131").Should().Be(700m);
        Row(s, "211").Should().Be(300m);
        Row(s, "311").Should().Be(600m);
        Row(s, "331").Should().Be(200m);
        Row(s, "270").Should().Be(1000m);
        s.IsBalanced.Should().BeTrue();
    }

    // ---- B03, both methods ----

    private sealed record Line(string Account, decimal Debit, decimal Credit);

    private sealed record Entry(bool InRange, params Line[] Lines);

    private static Line Dr(string a, decimal v) => new(a, v, 0m);

    private static Line Cr(string a, decimal v) => new(a, 0m, v);

    private List<Entry> Ledger() => new()
    {
        new(false, Dr("1000", 2000m), Cr("3000", 2000m)),                   // capital before the range
        new(true, Dr("1100", 1000m), Cr("4000", 1000m)),                    // sale on credit
        new(true, Dr("1000", 600m), Cr("1100", 600m)),                      // 600 collected
        new(true, Dr("1200", 500m), Cr("2000", 500m)),                      // stock on credit
        new(true, Dr("2000", 200m), Cr("1000", 200m)),                      // 200 paid
        new(true, Dr("5000", 300m), Cr("1200", 300m)),                      // cost of goods sold
        new(true, Dr("5200", 400m), Cr("2100", 400m)),                      // wages accrued
        new(true, Dr("2100", 400m), Cr("1000", 400m)),                      // wages paid
        new(true, Dr("5100", 100m), Cr("2200", 100m)),                      // expense with tax payable
        new(true, Dr("2200", 40m), Cr("1000", 40m)),                        // tax paid
        new(true, Dr("1500", 800m), Cr("1000", 800m)),                      // equipment for cash
        new(true, Dr("3000", 0m), Cr("3000", 0m)),
    };

    private static Dictionary<Guid, AccountBalanceSummary> Sum(IEnumerable<Entry> entries, Func<string, Account> a)
    {
        var result = new Dictionary<Guid, AccountBalanceSummary>();
        foreach (var l in entries.SelectMany(e => e.Lines))
        {
            var id = a(l.Account).Id;
            if (!result.TryGetValue(id, out var b))
                result[id] = b = new AccountBalanceSummary { AccountId = id };
            b.TotalDebit += l.Debit;
            b.TotalCredit += l.Credit;
        }
        return result;
    }

    private CashFlowColumn DirectColumn(List<Entry> ledger)
    {
        var movements = new List<CashMovement>();
        foreach (var e in ledger.Where(e => e.InRange && e.Lines.Any(l => l.Account == "1000")))
            foreach (var l in e.Lines.Where(l => l.Account != "1000"))
                movements.Add(new CashMovement(A(l.Account), l.Credit - l.Debit));
        decimal Cash(IEnumerable<Entry> es) => es.SelectMany(e => e.Lines).Where(l => l.Account == "1000").Sum(l => l.Debit - l.Credit);
        return new CashFlowColumn(movements, Cash(ledger.Where(e => !e.InRange)), Cash(ledger));
    }

    [Fact]
    public void The_direct_cash_flow_places_each_movement_on_a_row_and_reconciles()
    {
        var s = CashFlowStatementEngine.Evaluate(CashFlowLayouts.For("Simple14"), DirectColumn(Ledger()), null);

        Row(s, "01").Should().Be(600m);     // collected from customers
        Row(s, "02").Should().Be(-200m);    // paid to the supplier
        Row(s, "03").Should().Be(-400m);    // wages
        Row(s, "05").Should().Be(-40m);     // tax paid
        Row(s, "20").Should().Be(-40m);
        Row(s, "21").Should().Be(-800m);    // equipment
        Row(s, "30").Should().Be(-800m);
        Row(s, "50").Should().Be(-840m);
        Row(s, "60").Should().Be(2000m);
        Row(s, "70").Should().Be(1160m);
        s.IsBalanced.Should().BeTrue();
        s.Unmapped.Should().BeEmpty();
    }

    [Fact]
    public void The_indirect_cash_flow_agrees_with_the_direct_one_on_the_14_account_chart()
    {
        var ledger = Ledger();
        var column = new IndirectColumn(
            DirectColumn(ledger),
            Sum(ledger.Where(e => !e.InRange), A),
            Sum(ledger, A),
            Sum(ledger.Where(e => e.InRange), A));

        var indirect = IndirectCashFlowEngine.Evaluate(IndirectCashFlowLayouts.For("Simple14"), _s14, column, null);
        var direct = CashFlowStatementEngine.Evaluate(CashFlowLayouts.For("Simple14"), column.Direct, null);

        // profit before tax 1,000 − 300 − 400 − 100 = 200; receivables +400 → −400; inventory +200 → −200; payables 300 + 0 + 60 → +360
        Row(indirect, "01").Should().Be(200m);
        Row(indirect, "09").Should().Be(-400m);
        Row(indirect, "10").Should().Be(-200m);
        Row(indirect, "11").Should().Be(360m);
        Row(indirect, "20").Should().Be(Row(direct, "20"));
        Row(indirect, "70").Should().Be(Row(direct, "70"));
        indirect.IsBalanced.Should().BeTrue();
        indirect.LayoutId.Should().Be("B03-DN-GT");
    }
}

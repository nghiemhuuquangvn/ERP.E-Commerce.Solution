using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-2g: B03-DN by the indirect method, reconciled with the direct method.</summary>
public class IndirectCashFlowTests
{
    private readonly List<Account> _chart = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();

    private Account A(string number) => _chart.Single(a => a.AccountNumber == number);

    // ---- a small double-entry ledger that produces both the cash movements (direct) and the balances (indirect) ----

    private sealed record Line(string Account, decimal Debit, decimal Credit);

    private sealed record Entry(bool InRange, params Line[] Lines);

    private static Entry Before(params Line[] lines) => new(false, lines);

    private static Entry In(params Line[] lines) => new(true, lines);

    private static Line Dr(string account, decimal amount) => new(account, amount, 0m);

    private static Line Cr(string account, decimal amount) => new(account, 0m, amount);

    private Dictionary<Guid, AccountBalanceSummary> Sum(IEnumerable<Entry> entries)
    {
        var result = new Dictionary<Guid, AccountBalanceSummary>();
        foreach (var l in entries.SelectMany(e => e.Lines))
        {
            var id = A(l.Account).Id;
            if (!result.TryGetValue(id, out var b))
                result[id] = b = new AccountBalanceSummary { AccountId = id };
            b.TotalDebit += l.Debit;
            b.TotalCredit += l.Credit;
        }
        return result;
    }

    private IndirectColumn Column(IReadOnlyList<Entry> ledger)
    {
        var cashNumbers = new[] { "1111", "1121" };
        var movements = new List<CashMovement>();
        foreach (var entry in ledger.Where(e => e.InRange && e.Lines.Any(l => cashNumbers.Contains(l.Account))))
            foreach (var line in entry.Lines.Where(l => !cashNumbers.Contains(l.Account)))
                movements.Add(new CashMovement(A(line.Account), line.Credit - line.Debit));

        decimal Cash(IEnumerable<Entry> entries) => entries.SelectMany(e => e.Lines).Where(l => cashNumbers.Contains(l.Account)).Sum(l => l.Debit - l.Credit);
        var direct = new CashFlowColumn(movements, Cash(ledger.Where(e => !e.InRange)), Cash(ledger));
        var profitAndLoss = Sum(ledger.Where(e => e.InRange));
        return new IndirectColumn(direct, Sum(ledger.Where(e => !e.InRange)), Sum(ledger), profitAndLoss);
    }

    private FinancialStatementDto Run(IReadOnlyList<Entry> ledger, decimal divisor = 1m, IReadOnlyList<Entry>? prior = null)
    {
        foreach (var a in _chart.Where(a => a.AccountNumber is "1111" or "1121"))
            a.SetCashFlow(true, null);
        return IndirectCashFlowEngine.Evaluate(IndirectCashFlowLayouts.Layout, _chart, Column(ledger), prior == null ? null : Column(prior), divisor);
    }

    private static decimal Row(FinancialStatementDto s, string code) => s.Rows.Single(r => r.Code == code).Amount;

    /// <summary>A year of ordinary business: sales on credit, purchases on credit, wages, depreciation, interest, tax, equipment, a loan.</summary>
    private static List<Entry> Ordinary() => new()
    {
        Before(Dr("1111", 5000m), Cr("4111", 5000m)),                        // capital, before the range
        In(Dr("131", 1000m), Cr("5111", 1000m)),                              // sale on credit
        In(Dr("1111", 600m), Cr("131", 600m)),                                // 600 collected
        In(Dr("1561", 500m), Cr("331", 500m)),                                // stock bought on credit
        In(Dr("331", 200m), Cr("1111", 200m)),                                // 200 paid to the supplier
        In(Dr("632", 300m), Cr("1561", 300m)),                                // cost of goods sold
        In(Dr("6421", 400m), Cr("334", 400m)),                                // wages accrued
        In(Dr("334", 400m), Cr("1111", 400m)),                                // wages paid
        In(Dr("6421", 100m), Cr("2141", 100m)),                               // depreciation
        In(Dr("635", 50m), Cr("1111", 50m)),                                  // interest paid
        In(Dr("1111", 20m), Cr("515", 20m)),                                  // interest received
        In(Dr("8211", 40m), Cr("3334", 40m)),                                 // income tax
        In(Dr("3334", 40m), Cr("1111", 40m)),                                 // income tax paid
        In(Dr("211", 800m), Cr("1111", 800m)),                               // equipment for cash
        In(Dr("1111", 2000m), Cr("341", 2000m)),                             // bank loan
    };

    [Fact]
    public void Operating_rows_start_from_profit_add_back_non_cash_items_and_follow_working_capital()
    {
        var s = Run(Ordinary());

        Row(s, "01").Should().Be(170m);    // 1,000 + 20 − 300 − 500 − 50
        Row(s, "02").Should().Be(100m);    // accumulated depreciation grew by 100
        Row(s, "05").Should().Be(-20m);    // interest received is an investing item
        Row(s, "06").Should().Be(50m);     // interest expense added back
        Row(s, "08").Should().Be(300m);
        Row(s, "09").Should().Be(-400m);   // receivables up 400
        Row(s, "10").Should().Be(-200m);   // inventory up 200
        Row(s, "11").Should().Be(300m);    // payables up 300 (supplier 500 − 200; wages nil)
        Row(s, "14").Should().Be(-50m);    // interest paid (cash)
        Row(s, "15").Should().Be(-40m);    // income tax paid (cash)
        Row(s, "20").Should().Be(-90m);
    }

    [Fact]
    public void Investing_and_financing_rows_are_the_cash_rows_of_the_direct_method_and_the_statement_reconciles()
    {
        var s = Run(Ordinary());

        Row(s, "21").Should().Be(-800m);
        Row(s, "27").Should().Be(20m);
        Row(s, "30").Should().Be(-780m);
        Row(s, "33").Should().Be(2000m);
        Row(s, "40").Should().Be(2000m);
        Row(s, "50").Should().Be(1130m);   // −90 − 780 + 2,000
        Row(s, "60").Should().Be(5000m);
        Row(s, "70").Should().Be(6130m);
        s.IsBalanced.Should().BeTrue();    // indirect 20 = direct 20, and 70 = the cash on the books
        s.Unmapped.Should().BeEmpty();
        s.LayoutId.Should().Be("B03-DN-GT");
    }

    [Fact]
    public void The_indirect_operating_total_equals_the_direct_one_for_the_same_ledger()
    {
        var ledger = Ordinary();
        var indirect = Run(ledger);
        var direct = CashFlowStatementEngine.Evaluate(CashFlowLayouts.Layout, Column(ledger).Direct, null);

        Row(indirect, "20").Should().Be(Row(direct, "20"));
        Row(indirect, "50").Should().Be(Row(direct, "50"));
        Row(indirect, "70").Should().Be(Row(direct, "70"));
    }

    [Fact]
    public void A_decrease_of_a_receivable_or_a_rise_of_a_payable_adds_cash_and_the_opposite_takes_it()
    {
        var ledger = new List<Entry>
        {
            Before(Dr("1111", 1000m), Cr("4111", 300m), Cr("331", 700m), Dr("131", 0m)),
            Before(Dr("131", 400m), Cr("5111", 400m)),                       // a receivable of 400 and revenue before the range
            In(Dr("1111", 400m), Cr("131", 400m)),                            // collected: receivables −400
            In(Dr("331", 100m), Cr("1111", 100m)),                            // payables −100
        };

        var s = Run(ledger);

        Row(s, "01").Should().Be(0m);
        Row(s, "09").Should().Be(400m);
        Row(s, "11").Should().Be(-100m);
        Row(s, "20").Should().Be(300m);
        s.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void Equipment_bought_on_credit_is_a_non_cash_item_and_the_difference_is_reported_not_hidden()
    {
        var ledger = new List<Entry>
        {
            Before(Dr("1111", 1000m), Cr("4111", 1000m)),
            In(Dr("211", 800m), Cr("331", 800m)),                           // equipment on credit: no cash, but payables rise
        };

        var s = Run(ledger);

        Row(s, "11").Should().Be(800m);    // the form counts the rise of payables as operating ...
        Row(s, "20").Should().Be(800m);    // ... while the direct method has no cash movement
        s.IsBalanced.Should().BeFalse();
        s.Notes.Should().Contain(n => n.StartsWith("Indirect operating cash flow (20) differs from the direct method by 800"));
    }

    [Fact]
    public void A_balance_sheet_account_the_form_does_not_follow_is_listed_as_unmapped()
    {
        var ledger = new List<Entry>
        {
            Before(Dr("1111", 1000m), Cr("4111", 1000m)),
            In(Dr("1111", 50m), Cr("1388", 50m)),                            // 138 is followed (receivables) ...
            In(Dr("1111", 30m), Cr("3386", 30m)),                            // ... 3386 deposits received is under 338 (payables): followed
            In(Dr("244", 10m), Cr("356", 10m)),                            // 356 (science and technology fund) is not on the form
        };

        var unmapped = Run(ledger).Unmapped;

        unmapped.Select(u => u.AccountNumber).Should().Contain("356");
        unmapped.Select(u => u.AccountNumber).Should().NotContain("1388").And.NotContain("3386");
    }

    [Fact]
    public void Rounding_applies_to_the_leaf_rows_and_the_totals_add_the_rounded_rows()
    {
        var s = Run(Ordinary(), 1000m);

        s.Unit.Should().Be("thousand VND");
        Row(s, "01").Should().Be(0m);      // 170 → 0
        Row(s, "60").Should().Be(5m);      // 5,000 → 5
        Row(s, "33").Should().Be(2m);      // 2,000 → 2
        Row(s, "20").Should().Be(Row(s, "08") + Row(s, "09") + Row(s, "10") + Row(s, "11") + Row(s, "12") + Row(s, "13") + Row(s, "14") + Row(s, "15") + Row(s, "16") + Row(s, "17"));
        s.IsBalanced.Should().BeTrue();    // checks are on the exact amounts
    }

    [Fact]
    public void The_prior_column_is_computed_from_its_own_ledger()
    {
        var prior = new List<Entry>
        {
            Before(Dr("1111", 100m), Cr("4111", 100m)),
            In(Dr("1111", 50m), Cr("5111", 50m)),
        };

        var s = Run(Ordinary(), 1m, prior);

        s.Rows.Single(r => r.Code == "01").PriorAmount.Should().Be(50m);
        s.Rows.Single(r => r.Code == "20").PriorAmount.Should().Be(50m);
        s.Rows.Single(r => r.Code == "70").PriorAmount.Should().Be(150m);
        s.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void The_layout_is_consistent()
    {
        var rows = IndirectCashFlowLayouts.Layout.Rows;
        rows.Select(r => r.Code).Should().OnlyHaveUniqueItems();
        var codes = rows.Select(r => r.Code).ToList();
        foreach (var (row, i) in rows.Select((r, i) => (r, i)).Where(x => x.r.Formula.Count > 0))
            row.Formula.Select(p => codes.IndexOf(p.Code)).Should().OnlyContain(j => j >= 0 && j < i);
        // the investing / financing rows are the same codes as the direct form
        var direct = CashFlowLayouts.Layout.Rows.Select(r => r.Code).ToHashSet();
        rows.Where(r => r.Source == IndirectSource.Direct && r.Code is not ("14" or "15")).Select(r => r.Code).Should().OnlyContain(c => direct.Contains(c));
    }

    // ---- the handler: method selection ----

    private (GetCashFlowStatementQueryHandler Handler, Mock<IJournalLineRepository> Lines) Handler()
    {
        var cash = A("1111");
        cash.SetCashFlow(true, null);
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _chart.ToList());
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(l => l.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountBalanceSummary> { new() { AccountId = cash.Id, TotalDebit = 100m } });
        lines.Setup(l => l.GetActivityBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountBalanceSummary>());
        lines.Setup(l => l.GetCashEntryLinesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CashEntryLine>());
        var profiles = new Mock<IReportProfileRepository>();
        return (new GetCashFlowStatementQueryHandler(accounts.Object, lines.Object, profiles.Object, new Mock<IStatementMappingRepository>().Object), lines);
    }

    private static GetCashFlowStatementQuery Query(string? method) =>
        new() { FromDate = new DateTime(2026, 10, 1), ToDate = new DateTime(2026, 10, 31), Method = method };

    [Theory]
    [InlineData(null, "B03-DN")]
    [InlineData("", "B03-DN")]
    [InlineData("direct", "B03-DN")]
    [InlineData("DIRECT", "B03-DN")]
    [InlineData("indirect", "B03-DN-GT")]
    [InlineData("Indirect", "B03-DN-GT")]
    public async Task The_method_selects_the_layout(string? method, string layout)
    {
        var (handler, lines) = Handler();

        var s = await handler.Handle(Query(method), default);

        s.LayoutId.Should().Be(layout);
        if (layout == "B03-DN-GT")
            lines.Verify(l => l.GetActivityBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), "PeriodClosing", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task An_unknown_method_is_refused()
    {
        var (handler, _) = Handler();

        await FluentActions.Awaiting(() => handler.Handle(Query("sideways"), default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*direct or indirect*");
    }
}

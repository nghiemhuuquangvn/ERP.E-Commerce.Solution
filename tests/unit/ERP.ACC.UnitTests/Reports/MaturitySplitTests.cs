using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-2h: the short- / long-term split of trade receivables and payables on B01, by the due date of the open documents.</summary>
public class MaturitySplitTests
{
    private static readonly DateTime AsOf = new(2026, 10, 31);
    private readonly List<Account> _chart = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();

    private Account A(string number) => _chart.Single(a => a.AccountNumber == number);

    private static Invoice Inv(decimal amount, DateTime due, int status = (int)InvoiceStatus.Unpaid, decimal paid = 0m, DateTime? created = null)
    {
        var i = Invoice.Create("INV", Guid.NewGuid(), Guid.NewGuid(), amount, due);
        i.Status = status;
        i.AmountPaid = paid;
        i.CreatedAt = created ?? AsOf.AddMonths(-3);
        return i;
    }

    private static Bill Bll(decimal amount, DateTime due, int status = (int)BillStatus.Approved, DateTime? created = null)
    {
        var b = Bill.Create("BILL", Guid.NewGuid(), Guid.NewGuid(), amount, due);
        b.Status = status;
        b.CreatedAt = created ?? AsOf.AddMonths(-3);
        return b;
    }

    private static StatementMaturity Split(IEnumerable<Invoice> invoices, IEnumerable<Bill> bills, decimal receivable = 10_000m, decimal payable = 10_000m) =>
        MaturitySplit.Compute(invoices, bills, AsOf, receivable, payable);

    // ---- the rule ----

    [Fact]
    public void A_due_date_more_than_twelve_months_after_the_date_is_long_term_and_exactly_twelve_months_is_short_term()
    {
        var m = Split(new[] { Inv(300m, AsOf.AddMonths(12).AddDays(1)), Inv(500m, AsOf.AddMonths(12)), Inv(700m, AsOf.AddMonths(3)) }, Array.Empty<Bill>());

        m.Receivable.Should().Be(300m);
    }

    [Fact]
    public void Only_the_open_amount_of_an_unpaid_or_overdue_invoice_counts()
    {
        var far = AsOf.AddMonths(18);
        var m = Split(new[]
        {
            Inv(1000m, far, paid: 400m),                            // 600 open
            Inv(200m, far, status: (int)InvoiceStatus.Overdue),     // 200 open
            Inv(900m, far, status: (int)InvoiceStatus.Paid),        // settled
            Inv(800m, far, status: (int)InvoiceStatus.Cancelled),   // void
        }, Array.Empty<Bill>());

        m.Receivable.Should().Be(800m);
    }

    [Fact]
    public void Only_an_approved_unpaid_bill_is_a_payable()
    {
        var far = AsOf.AddMonths(15);
        var m = Split(Array.Empty<Invoice>(), new[]
        {
            Bll(400m, far),
            Bll(300m, far, status: (int)BillStatus.Pending),   // not in the ledger yet
            Bll(200m, far, status: (int)BillStatus.Paid),
            Bll(100m, AsOf.AddMonths(2)),                      // due soon: short-term
        });

        m.Payable.Should().Be(400m);
    }

    [Fact]
    public void A_document_created_after_the_date_did_not_exist_yet()
    {
        var far = AsOf.AddMonths(18);
        var m = Split(new[] { Inv(500m, far, created: AsOf.AddDays(1)), Inv(100m, far) }, new[] { Bll(50m, far, created: AsOf.AddDays(5)) });

        m.Receivable.Should().Be(100m);
        m.Payable.Should().Be(0m);
    }

    [Fact]
    public void The_split_is_capped_by_the_ledger_and_nothing_moves_without_documents()
    {
        var far = AsOf.AddMonths(18);

        var capped = Split(new[] { Inv(900m, far) }, new[] { Bll(900m, far) }, receivable: 400m, payable: 250m);
        capped.Receivable.Should().Be(400m);
        capped.Payable.Should().Be(250m);

        Split(Array.Empty<Invoice>(), Array.Empty<Bill>()).IsEmpty.Should().BeTrue();
        Split(new[] { Inv(900m, far) }, Array.Empty<Bill>(), receivable: -50m).Receivable.Should().Be(0m); // a credit balance on 131 has no receivable to split
    }

    // ---- the statement ----

    private Dictionary<Guid, AccountBalanceSummary> Balances(params (string Number, decimal Debit, decimal Credit)[] lines) =>
        lines.ToDictionary(l => A(l.Number).Id, l => new AccountBalanceSummary { AccountId = A(l.Number).Id, TotalDebit = l.Debit, TotalCredit = l.Credit });

    private static decimal Row(FinancialStatementDto s, string code) => s.Rows.Single(r => r.Code == code).Amount;

    private FinancialStatementDto B01(StatementMaturity? maturity, decimal divisor = 1m) =>
        StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.BalanceSheet)!, _chart,
            Balances(("131", 1000m, 0m), ("1111", 500m, 0m), ("331", 0m, 800m), ("4111", 0m, 700m)), null, divisor, null, maturity);

    [Fact]
    public void Long_term_amounts_move_to_their_own_rows_and_no_total_changes()
    {
        var before = B01(null);
        var after = B01(new StatementMaturity(300m, 200m));

        Row(before, "131").Should().Be(1000m);
        Row(before, "211").Should().Be(0m);

        Row(after, "131").Should().Be(700m);   // short-term trade receivables
        Row(after, "211").Should().Be(300m);   // long-term trade receivables
        Row(after, "210").Should().Be(300m);
        Row(after, "311").Should().Be(600m);   // short-term trade payables
        Row(after, "331").Should().Be(200m);   // long-term trade payables
        Row(after, "330").Should().Be(200m);

        // the split only moves amounts between rows: assets, liabilities and the check are unchanged
        Row(after, "270").Should().Be(Row(before, "270"));
        Row(after, "300").Should().Be(Row(before, "300"));
        Row(after, "440").Should().Be(Row(before, "440"));
        after.IsBalanced.Should().BeTrue();
        after.Notes.Should().Contain(n => n.StartsWith("Trade receivables and payables due after"));
        before.Notes.Should().NotContain(n => n.StartsWith("Trade receivables and payables due after"));
    }

    [Fact]
    public void Rounding_applies_to_each_part_and_the_totals_follow_the_rounded_rows()
    {
        var s = B01(new StatementMaturity(300m, 200m), 1000m);

        Row(s, "131").Should().Be(1m);   // 700 → 1
        Row(s, "211").Should().Be(0m);   // 300 → 0
        Row(s, "311").Should().Be(1m);   // 600 → 1
        Row(s, "331").Should().Be(0m);   // 200 → 0
        s.IsBalanced.Should().BeTrue();  // exact amounts agree
    }

    [Fact]
    public void The_prior_column_uses_its_own_maturity()
    {
        var s = StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.BalanceSheet)!, _chart,
            Balances(("131", 1000m, 0m), ("1111", 500m, 0m), ("331", 0m, 800m), ("4111", 0m, 700m)),
            Balances(("131", 600m, 0m), ("4111", 0m, 600m)), 1m, null, new StatementMaturity(300m, 0m), new StatementMaturity(0m, 0m));

        var r211 = s.Rows.Single(r => r.Code == "211");
        r211.Amount.Should().Be(300m);
        r211.PriorAmount.Should().Be(0m);
        s.Rows.Single(r => r.Code == "131").PriorAmount.Should().Be(600m);
    }

    [Fact]
    public void The_new_rows_are_part_of_the_layout_and_the_totals()
    {
        var rows = StatementLayouts.Find(StatementLayouts.BalanceSheet)!.Rows;

        rows.Select(r => r.Code).Should().OnlyHaveUniqueItems();
        rows.Single(r => r.Code == "200").Formula.Select(p => p.Code).Should().Contain("210");
        rows.Single(r => r.Code == "330").Formula.Select(p => p.Code).Should().Contain("331");
        rows.Single(r => r.Code == "131").Special.Should().Be("ShortLessLongReceivable");
        rows.Single(r => r.Code == "311").Special.Should().Be("ShortLessLongPayable");
    }

    // ---- the handler ----

    private GetBalanceSheetStatementQueryHandler Handler(DateTime date, IEnumerable<Invoice> invoices, IEnumerable<Bill> bills, out DateTime asOf)
    {
        asOf = date;
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _chart.ToList());
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(l => l.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Balances(("131", 1000m, 0m), ("1111", 500m, 0m), ("331", 0m, 800m), ("4111", 0m, 700m)).Values.ToList());
        var inv = new Mock<IInvoiceRepository>();
        inv.Setup(r => r.GetUnpaidInvoicesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(invoices.ToList());
        var bl = new Mock<IBillRepository>();
        bl.Setup(r => r.GetUnpaidBillsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(bills.ToList());
        return new GetBalanceSheetStatementQueryHandler(accounts.Object, lines.Object, new Mock<IReportProfileRepository>().Object, inv.Object, bl.Object);
    }

    [Fact]
    public async Task The_handler_splits_by_the_open_documents_and_stays_quiet_about_history_for_today()
    {
        var today = DateTime.UtcNow.Date;
        var handler = Handler(today, new[] { Inv(300m, today.AddMonths(18), created: today.AddMonths(-1)) },
            new[] { Bll(200m, today.AddMonths(14), created: today.AddMonths(-1)) }, out var asOf);

        var s = await handler.Handle(new GetBalanceSheetStatementQuery { AsOfDate = asOf }, default);

        Row(s, "211").Should().Be(300m);
        Row(s, "331").Should().Be(200m);
        Row(s, "131").Should().Be(700m);
        s.IsBalanced.Should().BeTrue();
        s.Notes.Should().NotContain(n => n.StartsWith("The date is in the past"));
    }

    [Fact]
    public async Task A_past_date_with_a_split_carries_the_approximation_note()
    {
        var date = new DateTime(2026, 6, 30);
        var handler = Handler(date, new[] { Inv(300m, date.AddMonths(18), created: date.AddMonths(-2)) }, Array.Empty<Bill>(), out var asOf);

        var s = await handler.Handle(new GetBalanceSheetStatementQuery { AsOfDate = asOf }, default);

        Row(s, "211").Should().Be(300m);
        s.Notes.Should().Contain(n => n.StartsWith("The date is in the past"));
    }

    [Fact]
    public async Task Without_long_term_documents_the_statement_is_the_same_as_before()
    {
        var handler = Handler(AsOf, new[] { Inv(300m, AsOf.AddMonths(2)) }, Array.Empty<Bill>(), out var asOf);

        var s = await handler.Handle(new GetBalanceSheetStatementQuery { AsOfDate = asOf }, default);

        Row(s, "131").Should().Be(1000m);
        Row(s, "211").Should().Be(0m);
        s.Notes.Should().NotContain(n => n.StartsWith("The date is in the past")).And.NotContain(n => n.StartsWith("Trade receivables and payables due after"));
    }
}

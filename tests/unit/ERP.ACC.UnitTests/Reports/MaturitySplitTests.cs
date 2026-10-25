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
        after.Notes.Should().Contain(n => n.StartsWith("Trade receivables, trade payables and borrowings due after"));
        before.Notes.Should().NotContain(n => n.StartsWith("Trade receivables, trade payables and borrowings due after"));
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

    private GetBalanceSheetStatementQueryHandler Handler(DateTime date, IEnumerable<Invoice> invoices, IEnumerable<Bill> bills, out DateTime asOf, IEnumerable<PaymentApplication>? payments = null)
    {
        asOf = date;
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _chart.ToList());
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(l => l.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Balances(("131", 1000m, 0m), ("1111", 500m, 0m), ("331", 0m, 800m), ("4111", 0m, 700m)).Values.ToList());
        var inv = new Mock<IInvoiceRepository>();
        inv.Setup(r => r.GetUnpaidInvoicesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(invoices.ToList());
        inv.Setup(r => r.GetMaturityCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(invoices.ToList());
        var bl = new Mock<IBillRepository>();
        bl.Setup(r => r.GetUnpaidBillsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(bills.ToList());
        bl.Setup(r => r.GetMaturityCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(bills.ToList());
        var ln = new Mock<ILoanRepository>();
        ln.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Loan>());
        return new GetBalanceSheetStatementQueryHandler(accounts.Object, lines.Object, new Mock<IReportProfileRepository>().Object, inv.Object, bl.Object, ln.Object, Payments(payments), new Mock<IStatementMappingRepository>().Object);
    }

    private static IPaymentApplicationRepository Payments(IEnumerable<PaymentApplication>? payments)
    {
        var repo = new Mock<IPaymentApplicationRepository>();
        repo.Setup(r => r.ListForMaturityAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((payments ?? Array.Empty<PaymentApplication>()).ToList());
        return repo.Object;
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
        s.Notes.Should().NotContain(n => n.Contains("approximate"));
    }

    // ---- payment history (ROADMAP P1-2j) ----

    private static PaymentApplication Pay(Invoice invoice, DateTime on, decimal amount, bool estimated = false) =>
        PaymentApplication.Create(PaymentApplication.InvoiceDocument, invoice.Id, on, amount, Guid.NewGuid().ToString("N"), estimated);

    private static PaymentApplication PayBill(Bill bill, DateTime on, decimal amount, bool estimated = false) =>
        PaymentApplication.Create(PaymentApplication.BillDocument, bill.Id, on, amount, Guid.NewGuid().ToString("N"), estimated);

    [Fact]
    public void An_invoice_paid_after_the_date_was_still_open_on_it_and_one_paid_before_was_not()
    {
        var far = AsOf.AddMonths(18);
        var paidLater = Inv(1000m, far, status: (int)InvoiceStatus.Paid, paid: 1000m);
        var paidBefore = Inv(400m, far, status: (int)InvoiceStatus.Paid, paid: 400m);
        var payments = new[] { Pay(paidLater, AsOf.AddDays(10), 1000m), Pay(paidBefore, AsOf.AddDays(-5), 400m) };

        var m = MaturitySplit.Compute(new[] { paidLater, paidBefore }, Array.Empty<Bill>(), payments, Array.Empty<Loan>(), AsOf, 10_000m, 10_000m, 0m);

        m.Receivable.Should().Be(1000m);
        m.Estimated.Should().BeFalse();
    }

    [Fact]
    public void A_partial_payment_before_the_date_reduces_the_open_amount_and_a_refund_after_it_changes_nothing()
    {
        var far = AsOf.AddMonths(18);
        var invoice = Inv(1000m, far, paid: 300m);
        var payments = new[] { Pay(invoice, AsOf.AddDays(-20), 700m), Pay(invoice, AsOf.AddDays(5), -400m) };

        var m = MaturitySplit.Compute(new[] { invoice }, Array.Empty<Bill>(), payments, Array.Empty<Loan>(), AsOf, 10_000m, 10_000m, 0m);

        m.Receivable.Should().Be(300m);
    }

    [Fact]
    public void A_cancellation_counts_from_its_day()
    {
        var far = AsOf.AddMonths(18);
        var cancelledLater = Inv(500m, far, status: (int)InvoiceStatus.Cancelled);
        cancelledLater.CancelledOn = AsOf.AddDays(3);
        var cancelledBefore = Inv(200m, far, status: (int)InvoiceStatus.Cancelled);
        cancelledBefore.CancelledOn = AsOf.AddDays(-3);

        var m = Split(new[] { cancelledLater, cancelledBefore }, Array.Empty<Bill>());

        m.Receivable.Should().Be(500m);
    }

    [Fact]
    public void A_bill_is_a_payable_from_its_approval_until_the_day_it_was_paid()
    {
        var far = AsOf.AddMonths(18);
        var approvedLater = Bll(100m, far, status: (int)BillStatus.Approved, created: AsOf.AddMonths(-3));
        approvedLater.ApprovedOn = AsOf.AddDays(4);
        var paidLater = Bll(300m, far, status: (int)BillStatus.Paid);
        var paidBefore = Bll(700m, far, status: (int)BillStatus.Paid);
        var payments = new[] { PayBill(paidLater, AsOf.AddDays(9), 300m), PayBill(paidBefore, AsOf.AddDays(-9), 700m) };

        var m = MaturitySplit.Compute(Array.Empty<Invoice>(), new[] { approvedLater, paidLater, paidBefore }, payments, Array.Empty<Loan>(), AsOf, 10_000m, 10_000m, 0m);

        m.Payable.Should().Be(300m);
    }

    [Fact]
    public void An_estimated_payment_or_a_missing_record_flags_the_split_as_approximate()
    {
        var far = new DateTime(2028, 12, 31);
        var estimated = Inv(400m, far, status: (int)InvoiceStatus.Paid, paid: 400m, created: new DateTime(2026, 5, 1));
        var noRecord = Inv(250m, far, status: (int)InvoiceStatus.Paid, paid: 250m, created: new DateTime(2026, 5, 1));
        var past = new DateTime(2026, 6, 30);

        MaturitySplit.Compute(new[] { estimated }, Array.Empty<Bill>(), new[] { Pay(estimated, past.AddDays(40), 400m, estimated: true) }, Array.Empty<Loan>(), past, 10_000m, 10_000m, 0m)
            .Estimated.Should().BeTrue();
        MaturitySplit.Compute(new[] { noRecord }, Array.Empty<Bill>(), Array.Empty<PaymentApplication>(), Array.Empty<Loan>(), past, 10_000m, 10_000m, 0m)
            .Estimated.Should().BeTrue();
    }

    [Fact]
    public async Task A_past_date_with_exact_payments_has_no_approximation_note_and_matches_the_split_of_that_day()
    {
        var date = new DateTime(2026, 6, 30);
        var invoice = Inv(300m, date.AddMonths(18), status: (int)InvoiceStatus.Paid, paid: 300m, created: date.AddMonths(-2));
        var handler = Handler(date, new[] { invoice }, Array.Empty<Bill>(), out var asOf, new[] { Pay(invoice, date.AddDays(30), 300m) });

        var s = await handler.Handle(new GetBalanceSheetStatementQuery { AsOfDate = asOf }, default);

        Row(s, "211").Should().Be(300m);
        s.Notes.Should().NotContain(n => n.Contains("approximate"));
    }

    [Fact]
    public async Task A_past_date_with_a_payment_that_has_no_record_carries_the_approximation_note()
    {
        var date = new DateTime(2026, 6, 30);
        var handler = Handler(date, new[] { Inv(300m, date.AddMonths(18), paid: 100m, created: date.AddMonths(-2)) }, Array.Empty<Bill>(), out var asOf);

        var s = await handler.Handle(new GetBalanceSheetStatementQuery { AsOfDate = asOf }, default);

        Row(s, "211").Should().Be(200m);
        s.Notes.Should().Contain(n => n.Contains("approximate"));
    }

    [Fact]
    public async Task Without_long_term_documents_the_statement_is_the_same_as_before()
    {
        var handler = Handler(AsOf, new[] { Inv(300m, AsOf.AddMonths(2)) }, Array.Empty<Bill>(), out var asOf);

        var s = await handler.Handle(new GetBalanceSheetStatementQuery { AsOfDate = asOf }, default);

        Row(s, "131").Should().Be(1000m);
        Row(s, "211").Should().Be(0m);
        s.Notes.Should().NotContain(n => n.Contains("approximate")).And.NotContain(n => n.StartsWith("Trade receivables, trade payables and borrowings due after"));
    }
}

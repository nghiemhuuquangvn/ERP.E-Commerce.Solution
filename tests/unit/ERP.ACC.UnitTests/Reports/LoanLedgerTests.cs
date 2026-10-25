using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-2i: the loan ledger and the short- / long-term split of borrowings on B01.</summary>
public class LoanLedgerTests
{
    private static readonly DateTime D = new(2026, 10, 31);
    private readonly List<Account> _chart = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();

    private Account A(string number) => _chart.Single(a => a.AccountNumber == number);

    /// <summary>A loan of 3,000 from 1 Jan 2026 repaid in three yearly installments of 1,000 (Jan 2027, Jan 2028, Jan 2029).</summary>
    private static Loan ThreeYearLoan(string number = "LN-1") => Loan.Create(number, "Vietcombank", 3000m, 8.5m, new DateTime(2026, 1, 1),
        new[] { (new DateTime(2027, 1, 15), 1000m, 100m), (new DateTime(2028, 1, 15), 1000m, 80m), (new DateTime(2029, 1, 15), 1000m, 60m) }, null, "working capital");

    // ---- the aggregate ----

    [Fact]
    public void A_loan_needs_a_number_a_lender_a_principal_and_a_schedule_that_adds_up()
    {
        FluentActions.Invoking(() => Loan.Create("", "Bank", 100m, 0m, D, null, D.AddYears(1), null)).Should().Throw<ArgumentException>().WithMessage("*number*");
        FluentActions.Invoking(() => Loan.Create("L", " ", 100m, 0m, D, null, D.AddYears(1), null)).Should().Throw<ArgumentException>().WithMessage("*lender*");
        FluentActions.Invoking(() => Loan.Create("L", "Bank", 0m, 0m, D, null, D.AddYears(1), null)).Should().Throw<ArgumentException>().WithMessage("*principal*");
        FluentActions.Invoking(() => Loan.Create("L", "Bank", 100m, 0m, D, null, null, null)).Should().Throw<ArgumentException>().WithMessage("*schedule or a maturity*");
        FluentActions.Invoking(() => Loan.Create("L", "Bank", 100m, 0m, D, new[] { (D.AddYears(1), 60m, 0m) }, null, null)).Should().Throw<ArgumentException>().WithMessage("*add up*");
        FluentActions.Invoking(() => Loan.Create("L", "Bank", 100m, 0m, D, new[] { (D.AddDays(-5), 100m, 0m) }, null, null)).Should().Throw<ArgumentException>().WithMessage("*after the start*");
    }

    [Fact]
    public void A_loan_without_a_schedule_is_one_installment_at_maturity()
    {
        var loan = Loan.Create("L", "Bank", 500m, 0m, D, null, D.AddMonths(18), null);

        loan.Installments.Should().ContainSingle().Which.PrincipalAmount.Should().Be(500m);
        loan.LongTermAt(D).Should().Be(500m);
    }

    [Fact]
    public void The_outstanding_amount_and_its_long_term_part_are_exact_for_any_date()
    {
        var loan = ThreeYearLoan();

        loan.OutstandingAt(new DateTime(2025, 12, 31)).Should().Be(0m);        // not started yet
        loan.OutstandingAt(D).Should().Be(3000m);
        loan.LongTermAt(D).Should().Be(2000m);                                  // 2028 and 2029 are later than 31 Oct 2027
        loan.LongTermAt(new DateTime(2027, 1, 20)).Should().Be(1000m);          // limit 20 Jan 2028: only 2029 is later
        loan.LongTermAt(new DateTime(2027, 1, 15)).Should().Be(1000m);          // 15 Jan 2028 is exactly 12 months: still short-term
    }

    [Fact]
    public void A_repayment_lowers_the_outstanding_from_its_own_date_and_not_before()
    {
        var loan = ThreeYearLoan();
        var first = loan.Installments.OrderBy(i => i.DueDate).First();

        loan.RecordRepayment(first.Id, new DateTime(2026, 7, 1));

        loan.OutstandingAt(new DateTime(2026, 6, 30)).Should().Be(3000m);       // before the repayment: still owed
        loan.OutstandingAt(new DateTime(2026, 7, 1)).Should().Be(2000m);
        loan.OutstandingAt(D).Should().Be(2000m);
        loan.LongTermAt(D).Should().Be(2000m);
    }

    [Fact]
    public void Repayment_rules_and_the_repaid_status()
    {
        var loan = ThreeYearLoan();
        var ids = loan.Installments.Select(i => i.Id).ToList();

        FluentActions.Invoking(() => loan.RecordRepayment(Guid.NewGuid(), D)).Should().Throw<InvalidOperationException>().WithMessage("*does not belong*");
        FluentActions.Invoking(() => loan.RecordRepayment(ids[0], new DateTime(2025, 12, 1))).Should().Throw<ArgumentException>().WithMessage("*before the loan started*");
        loan.RecordRepayment(ids[0], D);
        FluentActions.Invoking(() => loan.RecordRepayment(ids[0], D)).Should().Throw<InvalidOperationException>().WithMessage("*already repaid*");
        loan.Status.Should().Be((int)LoanStatus.Active);

        loan.RecordRepayment(ids[1], D);
        loan.RecordRepayment(ids[2], D);

        loan.Status.Should().Be((int)LoanStatus.Repaid);
        loan.OutstandingAt(D).Should().Be(0m);
        FluentActions.Invoking(() => loan.Cancel("x")).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_cancelled_loan_owes_nothing_and_cancelling_needs_a_reason_and_no_repayment()
    {
        var withPayment = ThreeYearLoan("A");
        withPayment.RecordRepayment(withPayment.Installments.First().Id, D);
        FluentActions.Invoking(() => withPayment.Cancel("typo")).Should().Throw<InvalidOperationException>().WithMessage("*repayments*");

        var loan = ThreeYearLoan("B");
        FluentActions.Invoking(() => loan.Cancel(" ")).Should().Throw<ArgumentException>().WithMessage("*reason*");
        loan.Cancel("entered twice");

        loan.Status.Should().Be((int)LoanStatus.Cancelled);
        loan.OutstandingAt(D).Should().Be(0m);
        loan.LongTermAt(D).Should().Be(0m);
        FluentActions.Invoking(() => loan.RecordRepayment(loan.Installments.First().Id, D)).Should().Throw<InvalidOperationException>().WithMessage("*Cancelled*");
    }

    // ---- the split ----

    [Fact]
    public void The_long_term_part_is_capped_by_the_ledger_balance_of_the_loan_account()
    {
        var loans = new[] { ThreeYearLoan("A"), ThreeYearLoan("B") };      // 2 × 2,000 long-term

        MaturitySplit.Compute(Array.Empty<Invoice>(), Array.Empty<Bill>(), loans, D, 0m, 0m, loanBalance: 5000m).Loan.Should().Be(4000m);
        MaturitySplit.Compute(Array.Empty<Invoice>(), Array.Empty<Bill>(), loans, D, 0m, 0m, loanBalance: 2500m).Loan.Should().Be(2500m);
        MaturitySplit.Compute(Array.Empty<Invoice>(), Array.Empty<Bill>(), loans, D, 0m, 0m, loanBalance: 0m).Loan.Should().Be(0m);
        MaturitySplit.Compute(Array.Empty<Invoice>(), Array.Empty<Bill>(), Array.Empty<Loan>(), D, 0m, 0m, 100m).IsEmpty.Should().BeTrue();
    }

    private Dictionary<Guid, AccountBalanceSummary> Balances(params (string Number, decimal Debit, decimal Credit)[] lines) =>
        lines.ToDictionary(l => A(l.Number).Id, l => new AccountBalanceSummary { AccountId = A(l.Number).Id, TotalDebit = l.Debit, TotalCredit = l.Credit });

    private static decimal Row(FinancialStatementDto s, string code) => s.Rows.Single(r => r.Code == code).Amount;

    [Fact]
    public void B01_moves_the_long_term_loan_to_its_own_row_and_no_total_changes()
    {
        var balances = Balances(("1121", 3500m, 0m), ("341", 0m, 3000m), ("343", 0m, 500m), ("4111", 0m, 0m), ("1561", 0m, 0m));
        // cash 3,500 against loans 3,000 + bonds 500: assets = liabilities
        var before = StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.BalanceSheet)!, _chart, balances, null);
        var after = StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.BalanceSheet)!, _chart, balances, null, 1m, null, new StatementMaturity(0m, 0m, 2000m));

        Row(before, "320").Should().Be(3000m);
        Row(before, "338").Should().Be(500m);
        Row(after, "320").Should().Be(1000m);     // short-term borrowings: the instalment due in 2027
        Row(after, "338").Should().Be(2500m);     // long-term: the bonds 343 plus the loan part due later
        Row(after, "310").Should().Be(Row(before, "310") - 2000m);
        Row(after, "330").Should().Be(Row(before, "330") + 2000m);
        Row(after, "300").Should().Be(Row(before, "300"));
        Row(after, "270").Should().Be(Row(before, "270"));
        after.IsBalanced.Should().BeTrue();
        after.Notes.Should().Contain(n => n.StartsWith("Trade receivables, trade payables and borrowings due after"));
    }

    // ---- the statement handler ----

    private GetBalanceSheetStatementQueryHandler Handler(DateTime date, IEnumerable<Loan> loans, decimal loanLedger = 3000m)
    {
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _chart.ToList());
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(l => l.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Balances(("1121", loanLedger, 0m), ("341", 0m, loanLedger)).Values.ToList());
        var inv = new Mock<IInvoiceRepository>();
        inv.Setup(r => r.GetUnpaidInvoicesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Invoice>());
        var bl = new Mock<IBillRepository>();
        bl.Setup(r => r.GetUnpaidBillsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Bill>());
        var ln = new Mock<ILoanRepository>();
        ln.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(loans.ToList());
        return new GetBalanceSheetStatementQueryHandler(accounts.Object, lines.Object, new Mock<IReportProfileRepository>().Object, inv.Object, bl.Object, ln.Object, new Mock<IPaymentApplicationRepository>().Object, new Mock<IStatementMappingRepository>().Object);
    }

    [Fact]
    public async Task The_handler_splits_borrowings_by_the_schedule_and_a_past_date_is_exact_without_the_approximation_note()
    {
        var s = await Handler(D, new[] { ThreeYearLoan() }).Handle(new GetBalanceSheetStatementQuery { AsOfDate = D }, default);

        Row(s, "320").Should().Be(1000m);
        Row(s, "338").Should().Be(2000m);
        s.IsBalanced.Should().BeTrue();
        s.Notes.Should().NotContain(n => n.StartsWith("The date is in the past"));   // loans keep the date of each repayment: exact
    }

    [Fact]
    public async Task A_loan_not_yet_in_the_ledger_is_capped_so_the_sheet_never_shows_more_than_the_books_hold()
    {
        var s = await Handler(D, new[] { ThreeYearLoan() }, loanLedger: 1500m).Handle(new GetBalanceSheetStatementQuery { AsOfDate = D }, default);

        Row(s, "338").Should().Be(1500m);
        Row(s, "320").Should().Be(0m);
    }

    // ---- the loan handlers ----

    private readonly List<Loan> _stored = new();

    private Mock<IAccountingUnitOfWork> Uow()
    {
        var repo = new Mock<ILoanRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<Loan>(), It.IsAny<CancellationToken>())).Callback<Loan, CancellationToken>((l, _) => _stored.Add(l)).Returns(Task.CompletedTask);
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _stored.ToList());
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => _stored.FirstOrDefault(l => l.Id == id));
        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.Loans).Returns(repo.Object);
        return uow;
    }

    private static CreateLoanCommand NewLoan(string number = "LN-9") => new()
    {
        LoanNumber = number, Lender = "ACB", Principal = 2000m, InterestRatePercent = 9m, StartDate = new DateTime(2026, 3, 1),
        Schedule = new() { new() { DueDate = new DateTime(2027, 3, 1), PrincipalAmount = 1000m, InterestAmount = 90m }, new() { DueDate = new DateTime(2028, 9, 1), PrincipalAmount = 1000m, InterestAmount = 45m } },
    };

    [Fact]
    public async Task Creating_a_loan_returns_its_outstanding_and_the_split_and_refuses_a_duplicate_number_or_a_bad_schedule()
    {
        var uow = Uow();
        var create = new CreateLoanCommandHandler(uow.Object, new Mock<IJournalPostingService>().Object, NullLogger<CreateLoanCommandHandler>.Instance);

        var dto = await create.Handle(NewLoan(), default);

        dto.Status.Should().Be("Active");
        dto.Outstanding.Should().Be(2000m);
        dto.Installments.Should().HaveCount(2);
        dto.DueWithinTwelveMonths.Should().Be(dto.Outstanding - dto.DueLater);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        await FluentActions.Awaiting(() => create.Handle(NewLoan("ln-9"), default)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*already exists*");
        var bad = NewLoan("LN-10");
        bad.Principal = 5000m;
        await FluentActions.Awaiting(() => create.Handle(bad, default)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*add up*");
    }

    [Fact]
    public async Task Repaying_and_cancelling_go_through_the_aggregate_rules()
    {
        var uow = Uow();
        var dto = await new CreateLoanCommandHandler(uow.Object, new Mock<IJournalPostingService>().Object, NullLogger<CreateLoanCommandHandler>.Instance).Handle(NewLoan(), default);
        var repay = new RecordLoanRepaymentCommandHandler(uow.Object, new Mock<IJournalPostingService>().Object, NullLogger<RecordLoanRepaymentCommandHandler>.Instance);

        var after = await repay.Handle(new RecordLoanRepaymentCommand { LoanId = dto.Id, InstallmentId = dto.Installments[0].Id, PaidOn = new DateTime(2026, 9, 1) }, default);

        after.Installments[0].PaidOn.Should().Be(new DateTime(2026, 9, 1));
        await FluentActions.Awaiting(() => repay.Handle(new RecordLoanRepaymentCommand { LoanId = dto.Id, InstallmentId = dto.Installments[0].Id }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*already repaid*");
        await FluentActions.Awaiting(() => new CancelLoanCommandHandler(uow.Object, new Mock<IJournalPostingService>().Object, NullLogger<CancelLoanCommandHandler>.Instance).Handle(new CancelLoanCommand { LoanId = dto.Id, Reason = "oops" }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*repayments*");
        await FluentActions.Awaiting(() => repay.Handle(new RecordLoanRepaymentCommand { LoanId = Guid.NewGuid(), InstallmentId = dto.Installments[1].Id }, default))
            .Should().ThrowAsync<KeyNotFoundException>();
    }
}

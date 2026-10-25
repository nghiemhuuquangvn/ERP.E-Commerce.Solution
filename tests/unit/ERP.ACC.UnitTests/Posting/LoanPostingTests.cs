using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-2j: the loan ledger posts the drawdown and the repayments, and is reconciled against the Loan payable account.</summary>
public class LoanPostingTests
{
    private static Dictionary<string, (decimal Dr, decimal Cr)> ByKey(PostingRequest r) =>
        r.Lines.GroupBy(l => l.PostingKey!).ToDictionary(g => g.Key, g => (g.Sum(l => l.Debit), g.Sum(l => l.Credit)));

    private static Loan TwoInstallmentLoan(string number = "LN-1") => Loan.Create(number, "ACB", 2000m, 9m, new DateTime(2026, 3, 1),
        new[] { (new DateTime(2027, 3, 1), 1000m, 90m), (new DateTime(2028, 3, 1), 1000m, 45m) }, null, null);

    // ---- the rules ----

    [Fact]
    public void A_drawdown_debits_the_bank_and_credits_the_loan_payable_on_the_start_date()
    {
        var loan = TwoInstallmentLoan();

        var request = PostingRules.ForLoanDrawdown(loan);

        request.SourceType.Should().Be("Loan");
        request.SourceId.Should().Be(loan.Id);
        request.Date.Should().Be(new DateTime(2026, 3, 1));
        var lines = ByKey(request);
        lines[PostingKeys.Bank].Should().Be((2000m, 0m));
        lines[PostingKeys.LoanPayable].Should().Be((0m, 2000m));
    }

    [Fact]
    public void A_repayment_splits_principal_and_interest_against_the_bank_and_is_booked_on_the_day_it_was_paid()
    {
        var loan = TwoInstallmentLoan();
        var installment = loan.RecordRepayment(loan.Installments.OrderBy(i => i.DueDate).First().Id, new DateTime(2027, 3, 5));

        var request = PostingRules.ForLoanRepayment(loan, installment, new DateTime(2027, 3, 5))!;

        request.SourceType.Should().Be("LoanRepayment");
        request.SourceId.Should().Be(installment.Id);
        request.Date.Should().Be(new DateTime(2027, 3, 5));
        var lines = ByKey(request);
        lines[PostingKeys.LoanPayable].Should().Be((1000m, 0m));
        lines[PostingKeys.InterestExpense].Should().Be((90m, 0m));
        lines[PostingKeys.Bank].Should().Be((0m, 1090m));
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public void An_installment_without_interest_has_no_interest_line()
    {
        var loan = Loan.Create("LN-2", "ACB", 500m, 0m, new DateTime(2026, 3, 1), new[] { (new DateTime(2027, 3, 1), 500m, 0m) }, null, null);
        var installment = loan.RecordRepayment(loan.Installments.Single().Id, new DateTime(2027, 3, 1));

        var request = PostingRules.ForLoanRepayment(loan, installment, new DateTime(2027, 3, 1))!;

        request.Lines.Should().NotContain(l => l.PostingKey == PostingKeys.InterestExpense);
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public void The_new_keys_have_a_default_account_in_both_charts()
    {
        PostingKeys.All.Should().Contain(new[] { PostingKeys.LoanPayable, PostingKeys.InterestExpense });
        PostingKeys.DefaultAccountNumbers[PostingKeys.LoanPayable].Should().Be("2100");
        ChartTemplates.Find(ChartTemplates.Vas99)!.PostingMap[PostingKeys.LoanPayable].Should().Be("341");
        ChartTemplates.Find(ChartTemplates.Vas99)!.PostingMap[PostingKeys.InterestExpense].Should().Be("635");
        foreach (var key in PostingKeys.All)
        {
            ChartTemplates.Find(ChartTemplates.Vas99)!.PostingMap.Should().ContainKey(key);
            PostingKeys.DefaultAccountNumbers.Should().ContainKey(key);
        }
    }

    // ---- the handlers ----

    private readonly List<Loan> _stored = new();
    private readonly List<PostingRequest> _posted = new();
    private readonly List<(string Type, Guid Id)> _reversed = new();
    private bool _profileComplete = true;

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

    private Mock<IJournalPostingService> Posting()
    {
        var posting = new Mock<IJournalPostingService>();
        posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostingRequest, CancellationToken>((r, _) => _posted.Add(r))
            .ReturnsAsync(() => _profileComplete ? JournalEntry.Create("JE-1", Guid.NewGuid(), "loan") : null);
        posting.Setup(p => p.ReverseAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid, string, DateTime, CancellationToken>((t, id, _, _, _) => _reversed.Add((t, id))).ReturnsAsync((JournalEntry?)null);
        return posting;
    }

    private static CreateLoanCommand NewLoan(bool post = true, string number = "LN-9") => new()
    {
        LoanNumber = number, Lender = "ACB", Principal = 2000m, InterestRatePercent = 9m, StartDate = new DateTime(2026, 3, 1), PostDrawdown = post,
        Schedule = new() { new() { DueDate = new DateTime(2027, 3, 1), PrincipalAmount = 1000m, InterestAmount = 90m }, new() { DueDate = new DateTime(2028, 3, 1), PrincipalAmount = 1000m, InterestAmount = 45m } },
    };

    private CreateLoanCommandHandler Create(Mock<IAccountingUnitOfWork> uow, Mock<IJournalPostingService> posting) =>
        new(uow.Object, posting.Object, NullLogger<CreateLoanCommandHandler>.Instance);

    private RecordLoanRepaymentCommandHandler Repay(Mock<IAccountingUnitOfWork> uow, Mock<IJournalPostingService> posting) =>
        new(uow.Object, posting.Object, NullLogger<RecordLoanRepaymentCommandHandler>.Instance);

    [Fact]
    public async Task Creating_a_loan_posts_the_drawdown_and_marks_it_in_the_ledger()
    {
        var uow = Uow();

        var dto = await Create(uow, Posting()).Handle(NewLoan(), default);

        dto.PostedToLedger.Should().BeTrue();
        _posted.Should().ContainSingle().Which.SourceType.Should().Be("Loan");
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_loan_booked_by_hand_posts_nothing_neither_the_drawdown_nor_its_repayments()
    {
        var uow = Uow();
        var posting = Posting();
        var dto = await Create(uow, posting).Handle(NewLoan(post: false), default);

        var after = await Repay(uow, posting).Handle(new RecordLoanRepaymentCommand { LoanId = dto.Id, InstallmentId = dto.Installments[0].Id, PaidOn = new DateTime(2027, 3, 1) }, default);

        dto.PostedToLedger.Should().BeFalse();
        after.Installments[0].PaidOn.Should().NotBeNull();
        _posted.Should().BeEmpty();
    }

    [Fact]
    public async Task An_incomplete_posting_profile_never_blocks_the_loan_it_just_stays_unposted()
    {
        _profileComplete = false;
        var uow = Uow();

        var dto = await Create(uow, Posting()).Handle(NewLoan(), default);

        dto.PostedToLedger.Should().BeFalse();
        _stored.Should().ContainSingle();
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_posted_loan_posts_each_repayment_with_its_interest()
    {
        var uow = Uow();
        var posting = Posting();
        var dto = await Create(uow, posting).Handle(NewLoan(), default);
        _posted.Clear();

        await Repay(uow, posting).Handle(new RecordLoanRepaymentCommand { LoanId = dto.Id, InstallmentId = dto.Installments[0].Id, PaidOn = new DateTime(2027, 3, 1) }, default);

        var request = _posted.Should().ContainSingle().Subject;
        request.SourceType.Should().Be("LoanRepayment");
        ByKey(request)[PostingKeys.Bank].Should().Be((0m, 1090m));
    }

    [Fact]
    public async Task Cancelling_a_posted_loan_reverses_the_drawdown_and_a_hand_booked_one_reverses_nothing()
    {
        var uow = Uow();
        var posting = Posting();
        var posted = await Create(uow, posting).Handle(NewLoan(), default);
        var cancel = new CancelLoanCommandHandler(uow.Object, posting.Object, NullLogger<CancelLoanCommandHandler>.Instance);

        await cancel.Handle(new CancelLoanCommand { LoanId = posted.Id, Reason = "entered twice" }, default);

        _reversed.Should().ContainSingle().Which.Should().Be(("Loan", posted.Id));

        var uow2 = Uow();
        var manual = await Create(uow2, posting).Handle(NewLoan(post: false, number: "LN-8"), default);
        _reversed.Clear();
        await new CancelLoanCommandHandler(uow2.Object, posting.Object, NullLogger<CancelLoanCommandHandler>.Instance)
            .Handle(new CancelLoanCommand { LoanId = manual.Id, Reason = "x" }, default);
        _reversed.Should().BeEmpty();
    }

    [Fact]
    public async Task An_older_loan_can_be_posted_later_but_not_twice_and_not_after_a_repayment()
    {
        var uow = Uow();
        var posting = Posting();
        var dto = await Create(uow, posting).Handle(NewLoan(post: false), default);
        var post = new PostLoanDrawdownCommandHandler(uow.Object, posting.Object, NullLogger<PostLoanDrawdownCommandHandler>.Instance);

        var after = await post.Handle(new PostLoanDrawdownCommand { LoanId = dto.Id }, default);

        after.PostedToLedger.Should().BeTrue();
        await FluentActions.Awaiting(() => post.Handle(new PostLoanDrawdownCommand { LoanId = dto.Id }, default)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*already posted*");

        var uow2 = Uow();
        var repaid = await Create(uow2, posting).Handle(NewLoan(post: false, number: "LN-8"), default);
        await Repay(uow2, posting).Handle(new RecordLoanRepaymentCommand { LoanId = repaid.Id, InstallmentId = repaid.Installments[0].Id, PaidOn = new DateTime(2027, 3, 1) }, default);
        await FluentActions.Awaiting(() => new PostLoanDrawdownCommandHandler(uow2.Object, posting.Object, NullLogger<PostLoanDrawdownCommandHandler>.Instance)
            .Handle(new PostLoanDrawdownCommand { LoanId = repaid.Id }, default)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*no repayment*");
    }

    // ---- the reconciliation ----

    private readonly List<Account> _chart = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();

    private GetLoanReconciliationQueryHandler Reconciler(IEnumerable<Loan> loans, decimal credit341, decimal debit341)
    {
        var account = _chart.Single(a => a.AccountNumber == "341");
        var loanRepo = new Mock<ILoanRepository>();
        loanRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(loans.ToList());
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_chart);
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(r => r.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountBalanceSummary> { new() { AccountId = account.Id, TotalCredit = credit341, TotalDebit = debit341 } });
        var mappings = new Mock<IPostingAccountMappingRepository>();
        mappings.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostingAccountMapping> { PostingAccountMapping.Create(Guid.NewGuid(), PostingKeys.LoanPayable, account.Id) });
        return new GetLoanReconciliationQueryHandler(loanRepo.Object, accounts.Object, lines.Object, mappings.Object);
    }

    [Fact]
    public async Task The_ledger_and_the_loan_payable_account_agree_when_everything_was_posted()
    {
        var loan = TwoInstallmentLoan();
        loan.MarkPosted();

        var result = await Reconciler(new[] { loan }, credit341: 2000m, debit341: 0m).Handle(new GetLoanReconciliationQuery { AsOfDate = new DateTime(2026, 10, 31) }, default);

        result.LedgerTotal.Should().Be(2000m);
        result.BookBalance.Should().Be(2000m);
        result.Difference.Should().Be(0m);
        result.IsReconciled.Should().BeTrue();
        result.AccountNumber.Should().Be("341");
    }

    [Fact]
    public async Task A_repayment_made_by_hand_in_the_books_but_not_in_the_ledger_shows_as_a_difference()
    {
        var loan = TwoInstallmentLoan();

        var result = await Reconciler(new[] { loan }, credit341: 2000m, debit341: 1000m).Handle(new GetLoanReconciliationQuery { AsOfDate = new DateTime(2026, 10, 31) }, default);

        result.Difference.Should().Be(1000m);
        result.IsReconciled.Should().BeFalse();
        result.Notes.Should().Contain(n => n.Contains("not posted"));
    }

    [Fact]
    public async Task A_cancelled_loan_and_a_loan_starting_after_the_date_are_not_counted()
    {
        var cancelled = TwoInstallmentLoan("LN-C");
        cancelled.Cancel("mistake");
        var later = Loan.Create("LN-L", "ACB", 700m, 5m, new DateTime(2026, 12, 1), new[] { (new DateTime(2027, 12, 1), 700m, 0m) }, null, null);

        var result = await Reconciler(new[] { cancelled, later }, 0m, 0m).Handle(new GetLoanReconciliationQuery { AsOfDate = new DateTime(2026, 10, 31) }, default);

        result.Loans.Should().BeEmpty();
        result.LedgerTotal.Should().Be(0m);
        result.IsReconciled.Should().BeTrue();
    }

    [Fact]
    public void The_reconciliation_exports_as_a_table_with_the_difference_row()
    {
        var table = ReportTables.LoanReconciliation(new LoanReconciliationDto
        {
            AsOfDate = new DateTime(2026, 10, 31), AccountNumber = "341", AccountName = "Loans", LedgerTotal = 2000m, BookBalance = 1500m, Difference = 500m,
            Loans = new() { new() { LoanNumber = "LN-1", Lender = "ACB", PostedToLedger = true, Outstanding = 2000m } },
        });

        table.Rows.Select(r => r[0]?.ToString()).Should().Contain(new[] { "Ledger total", "Book balance", "Difference" });
        table.Rows.Single(r => r[0]?.ToString() == "Difference")[2].Should().Be("NOT reconciled");
    }
}

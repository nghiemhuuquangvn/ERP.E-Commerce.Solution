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

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-2k: a receiving account per loan, accrued interest, interest actually paid.</summary>
public class LoanInterestTests
{
    private static Dictionary<string, (decimal Dr, decimal Cr)> ByKey(PostingRequest r) =>
        r.Lines.GroupBy(l => l.PostingKey ?? "account:" + l.AccountId).ToDictionary(g => g.Key, g => (g.Sum(l => l.Debit), g.Sum(l => l.Credit)));

    // 2,000 from 1 Nov 2026: installment 1 due 1 Dec (principal 1,000, interest 90), installment 2 due 1 Jun 2028 (1,000, interest 400)
    private static Loan NewLoan() => Loan.Create("LN-K", "ACB", 2000m, 9m, new DateTime(2026, 11, 1),
        new[] { (new DateTime(2026, 12, 1), 1000m, 90m), (new DateTime(2028, 6, 1), 1000m, 400m) }, null, null);

    // ---- receiving account ----

    [Fact]
    public void A_loan_with_a_receiving_account_uses_it_for_the_drawdown_and_the_repayment_instead_of_the_bank_key()
    {
        var loan = NewLoan();
        var account = Guid.NewGuid();
        loan.SetReceivingAccount(account);
        var first = loan.Installments.OrderBy(i => i.DueDate).First();
        loan.RecordRepayment(first.Id, new DateTime(2026, 12, 1));

        var draw = PostingRules.ForLoanDrawdown(loan);
        var repay = PostingRules.ForLoanRepayment(loan, first, new DateTime(2026, 12, 1))!;

        draw.Lines.Should().Contain(l => l.AccountId == account && l.Debit == 2000m && l.PostingKey == null);
        draw.Lines.Should().NotContain(l => l.PostingKey == PostingKeys.Bank);
        repay.Lines.Should().Contain(l => l.AccountId == account && l.Credit == 1090m);
        repay.Lines.Should().NotContain(l => l.PostingKey == PostingKeys.Bank);
    }

    [Fact]
    public void Without_a_receiving_account_the_bank_key_is_used_as_before()
    {
        PostingRules.ForLoanDrawdown(NewLoan()).Lines.Should().Contain(l => l.PostingKey == PostingKeys.Bank && l.Debit == 2000m);
    }

    // ---- accrual math ----

    [Fact]
    public void Interest_accrues_by_calendar_days_in_the_period_of_each_installment()
    {
        var loan = NewLoan();

        var half = loan.AccrueInterest(new DateTime(2026, 11, 16));   // 15 of the 30 days of November

        half.Should().ContainSingle().Which.Amount.Should().Be(45m);
        loan.Installments.OrderBy(i => i.DueDate).First().AccruedInterest.Should().Be(45m);
    }

    [Fact]
    public void Running_the_accrual_again_for_the_same_or_an_earlier_date_adds_nothing_and_a_later_date_adds_the_difference()
    {
        var loan = NewLoan();
        loan.AccrueInterest(new DateTime(2026, 11, 16));

        loan.AccrueInterest(new DateTime(2026, 11, 16)).Should().BeEmpty();
        loan.AccrueInterest(new DateTime(2026, 11, 10)).Should().BeEmpty();
        var later = loan.AccrueInterest(new DateTime(2026, 11, 22));   // 21/30 = 63 in total

        later.Should().ContainSingle().Which.Amount.Should().Be(18m);
    }

    [Fact]
    public void After_the_due_date_the_whole_interest_is_accrued_and_the_next_period_starts_at_the_previous_due_date()
    {
        var loan = NewLoan();

        var result = loan.AccrueInterest(new DateTime(2026, 12, 31));

        var ordered = result.OrderBy(r => r.Installment.DueDate).ToList();
        ordered[0].Amount.Should().Be(90m);                 // the whole of installment 1
        ordered[1].Amount.Should().BeGreaterThan(0m);       // 30 days of the long period (1 Dec 2026 → 1 Jun 2028 = 548 days): 400 × 30 / 548
        ordered[1].Amount.Should().Be(Math.Round(400m * 30m / 548m, 2));
    }

    [Fact]
    public void A_repaid_installment_and_a_cancelled_loan_do_not_accrue()
    {
        var loan = NewLoan();
        loan.RecordRepayment(loan.Installments.OrderBy(i => i.DueDate).First().Id, new DateTime(2026, 12, 1));

        loan.AccrueInterest(new DateTime(2026, 11, 20)).Should().BeEmpty();

        var cancelled = NewLoan();
        cancelled.Cancel("mistake");
        cancelled.AccrueInterest(new DateTime(2026, 11, 20)).Should().BeEmpty();
    }

    [Fact]
    public void A_loan_with_accrued_interest_cannot_be_cancelled()
    {
        var loan = NewLoan();
        loan.AccrueInterest(new DateTime(2026, 11, 16));

        FluentActions.Invoking(() => loan.Cancel("oops")).Should().Throw<InvalidOperationException>().WithMessage("*accrued*");
    }

    // ---- the entries ----

    [Fact]
    public void An_accrual_debits_the_interest_expense_and_credits_the_interest_payable_with_one_id_per_installment_and_date()
    {
        var loan = NewLoan();
        var installment = loan.Installments.First();

        var a = PostingRules.ForLoanAccrual(loan, installment, 45m, new DateTime(2026, 11, 16));
        var again = PostingRules.ForLoanAccrual(loan, installment, 45m, new DateTime(2026, 11, 16));
        var other = PostingRules.ForLoanAccrual(loan, installment, 18m, new DateTime(2026, 11, 22));

        a.SourceType.Should().Be("LoanAccrual");
        ByKey(a)[PostingKeys.InterestExpense].Should().Be((45m, 0m));
        ByKey(a)[PostingKeys.InterestPayable].Should().Be((0m, 45m));
        a.SourceId.Should().Be(again.SourceId);
        a.SourceId.Should().NotBe(other.SourceId);
    }

    [Fact]
    public void A_repayment_clears_the_accrual_and_books_only_the_rest_of_the_interest_to_expense()
    {
        var loan = NewLoan();
        var first = loan.Installments.OrderBy(i => i.DueDate).First();
        loan.AccrueInterest(new DateTime(2026, 11, 16));                              // 45 accrued
        loan.RecordRepayment(first.Id, new DateTime(2026, 12, 1), interestPaid: 100m);  // 100 actually paid

        var lines = ByKey(PostingRules.ForLoanRepayment(loan, first, new DateTime(2026, 12, 1))!);

        lines[PostingKeys.LoanPayable].Should().Be((1000m, 0m));
        lines[PostingKeys.InterestPayable].Should().Be((45m, 0m));
        lines[PostingKeys.InterestExpense].Should().Be((55m, 0m));
        lines[PostingKeys.Bank].Should().Be((0m, 1100m));
    }

    [Fact]
    public void When_less_interest_is_paid_than_was_accrued_the_expense_is_credited_back()
    {
        var loan = NewLoan();
        var first = loan.Installments.OrderBy(i => i.DueDate).First();
        loan.AccrueInterest(new DateTime(2026, 12, 1));                               // 90 accrued
        loan.RecordRepayment(first.Id, new DateTime(2026, 12, 1), interestPaid: 80m);

        var request = PostingRules.ForLoanRepayment(loan, first, new DateTime(2026, 12, 1))!;

        var lines = ByKey(request);
        lines[PostingKeys.InterestPayable].Should().Be((90m, 0m));
        lines[PostingKeys.InterestExpense].Should().Be((0m, 10m));
        lines[PostingKeys.Bank].Should().Be((0m, 1080m));
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public void Interest_paid_replaces_the_scheduled_interest_and_a_negative_amount_is_refused()
    {
        var loan = NewLoan();
        var first = loan.Installments.OrderBy(i => i.DueDate).First();

        FluentActions.Invoking(() => loan.RecordRepayment(first.Id, new DateTime(2026, 12, 1), interestPaid: -1m)).Should().Throw<ArgumentException>();
        loan.RecordRepayment(first.Id, new DateTime(2026, 12, 1), interestPaid: 70m);

        first.InterestAmount.Should().Be(70m);
    }

    // ---- handlers ----

    private readonly List<Loan> _stored = new();
    private readonly List<PostingRequest> _posted = new();
    private bool _profileComplete = true;
    private readonly Account _bank = Account.Create("1121", "Bank", (int)AccountType.Asset, (int)NormalBalance.Debit);
    private readonly Account _liability = Account.Create("341", "Loans", (int)AccountType.Liability, (int)NormalBalance.Credit);

    private Mock<IAccountingUnitOfWork> Uow()
    {
        var repo = new Mock<ILoanRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<Loan>(), It.IsAny<CancellationToken>())).Callback<Loan, CancellationToken>((l, _) => _stored.Add(l)).Returns(Task.CompletedTask);
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _stored.ToList());
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => _stored.FirstOrDefault(l => l.Id == id));
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(a => a.GetByIdAsync(_bank.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_bank);
        accounts.Setup(a => a.GetByIdAsync(_liability.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_liability);
        accounts.Setup(a => a.GetParentAccountIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HashSet<Guid>());
        accounts.Setup(a => a.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<Account> { _bank });
        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.Loans).Returns(repo.Object);
        uow.SetupGet(u => u.Accounts).Returns(accounts.Object);
        return uow;
    }

    private Mock<IJournalPostingService> Posting()
    {
        var posting = new Mock<IJournalPostingService>();
        posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostingRequest, CancellationToken>((r, _) => _posted.Add(r))
            .ReturnsAsync(() => _profileComplete ? JournalEntry.Create("JE-1", Guid.NewGuid(), "x") : null);
        return posting;
    }

    private static CreateLoanCommand Command(Guid? account = null, string number = "LN-H") => new()
    {
        LoanNumber = number, Lender = "ACB", Principal = 2000m, InterestRatePercent = 9m, StartDate = new DateTime(2026, 11, 1), ReceivingAccountId = account,
        Schedule = new() { new() { DueDate = new DateTime(2026, 12, 1), PrincipalAmount = 1000m, InterestAmount = 90m }, new() { DueDate = new DateTime(2028, 6, 1), PrincipalAmount = 1000m, InterestAmount = 400m } },
    };

    [Fact]
    public async Task Creating_a_loan_with_a_bank_account_stores_it_and_posts_to_it_and_other_accounts_are_refused()
    {
        var uow = Uow();
        var handler = new CreateLoanCommandHandler(uow.Object, Posting().Object, NullLogger<CreateLoanCommandHandler>.Instance);

        var dto = await handler.Handle(Command(_bank.Id), default);

        dto.ReceivingAccountId.Should().Be(_bank.Id);
        dto.ReceivingAccountNumber.Should().Be("1121");
        _posted.Single().Lines.Should().Contain(l => l.AccountId == _bank.Id);
        await FluentActions.Awaiting(() => handler.Handle(Command(_liability.Id, "LN-X"), default)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*not an asset account*");
        await FluentActions.Awaiting(() => handler.Handle(Command(Guid.NewGuid(), "LN-Y"), default)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*does not exist*");
    }

    [Fact]
    public async Task The_accrual_posts_the_delta_once_per_date_and_remembers_it()
    {
        var uow = Uow();
        var posting = Posting();
        await new CreateLoanCommandHandler(uow.Object, posting.Object, NullLogger<CreateLoanCommandHandler>.Instance).Handle(Command(), default);
        _posted.Clear();
        var accrue = new AccrueLoanInterestCommandHandler(uow.Object, posting.Object, NullLogger<AccrueLoanInterestCommandHandler>.Instance);

        var first = await accrue.Handle(new AccrueLoanInterestCommand { AsOfDate = new DateTime(2026, 11, 16) }, default);
        var again = await accrue.Handle(new AccrueLoanInterestCommand { AsOfDate = new DateTime(2026, 11, 16) }, default);

        first.TotalAccrued.Should().Be(45m);
        first.Lines.Should().ContainSingle().Which.Posted.Should().BeTrue();
        again.Lines.Should().BeEmpty();
        _posted.Should().ContainSingle().Which.SourceType.Should().Be("LoanAccrual");
        _stored.Single().Installments.OrderBy(i => i.DueDate).First().AccruedInterest.Should().Be(45m);
    }

    [Fact]
    public async Task A_hand_booked_loan_is_not_accrued_and_an_incomplete_profile_leaves_the_interest_un_accrued()
    {
        var uow = Uow();
        var posting = Posting();
        var create = new CreateLoanCommandHandler(uow.Object, posting.Object, NullLogger<CreateLoanCommandHandler>.Instance);
        var manual = Command(null, "LN-M");
        manual.PostDrawdown = false;
        await create.Handle(manual, default);
        var accrue = new AccrueLoanInterestCommandHandler(uow.Object, posting.Object, NullLogger<AccrueLoanInterestCommandHandler>.Instance);

        (await accrue.Handle(new AccrueLoanInterestCommand { AsOfDate = new DateTime(2026, 11, 16) }, default)).Lines.Should().BeEmpty();

        await create.Handle(Command(null, "LN-P"), default);
        _profileComplete = false;
        var failed = await accrue.Handle(new AccrueLoanInterestCommand { AsOfDate = new DateTime(2026, 11, 16) }, default);
        failed.Lines.Should().ContainSingle().Which.Posted.Should().BeFalse();
        failed.TotalAccrued.Should().Be(0m);
        _stored.Single(l => l.LoanNumber == "LN-P").Installments.Sum(i => i.AccruedInterest).Should().Be(0m);
    }

    [Fact]
    public async Task Repaying_with_the_interest_actually_paid_posts_the_difference_to_expense()
    {
        var uow = Uow();
        var posting = Posting();
        var dto = await new CreateLoanCommandHandler(uow.Object, posting.Object, NullLogger<CreateLoanCommandHandler>.Instance).Handle(Command(), default);
        await new AccrueLoanInterestCommandHandler(uow.Object, posting.Object, NullLogger<AccrueLoanInterestCommandHandler>.Instance)
            .Handle(new AccrueLoanInterestCommand { AsOfDate = new DateTime(2026, 11, 16) }, default);
        _posted.Clear();

        await new RecordLoanRepaymentCommandHandler(uow.Object, posting.Object, NullLogger<RecordLoanRepaymentCommandHandler>.Instance)
            .Handle(new RecordLoanRepaymentCommand { LoanId = dto.Id, InstallmentId = dto.Installments[0].Id, PaidOn = new DateTime(2026, 12, 1), InterestPaid = 100m }, default);

        var lines = ByKey(_posted.Single());
        lines[PostingKeys.InterestPayable].Should().Be((45m, 0m));
        lines[PostingKeys.InterestExpense].Should().Be((55m, 0m));
    }

    [Fact]
    public async Task Post_drawdown_can_set_the_receiving_account_of_an_older_loan()
    {
        var uow = Uow();
        var posting = Posting();
        var manual = Command(null, "LN-O");
        manual.PostDrawdown = false;
        var dto = await new CreateLoanCommandHandler(uow.Object, posting.Object, NullLogger<CreateLoanCommandHandler>.Instance).Handle(manual, default);

        var after = await new PostLoanDrawdownCommandHandler(uow.Object, posting.Object, NullLogger<PostLoanDrawdownCommandHandler>.Instance)
            .Handle(new PostLoanDrawdownCommand { LoanId = dto.Id, ReceivingAccountId = _bank.Id }, default);

        after.ReceivingAccountId.Should().Be(_bank.Id);
        _posted.Single().Lines.Should().Contain(l => l.AccountId == _bank.Id);
    }

    [Fact]
    public async Task The_reconciliation_compares_accrued_interest_with_the_interest_payable_account_when_it_is_a_separate_account()
    {
        var chart = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();
        var principalAccount = chart.Single(a => a.AccountNumber == "341");
        var interestAccount = chart.Single(a => a.AccountNumber == "335");
        var loan = NewLoan();
        loan.MarkPosted();
        loan.AccrueInterest(loan.StartDate.AddDays(15));   // 45 accrued
        var loans = new Mock<ILoanRepository>();
        loans.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Loan> { loan });
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(chart);
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(l => l.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountBalanceSummary>
            {
                new() { AccountId = principalAccount.Id, TotalCredit = 2000m }, new() { AccountId = interestAccount.Id, TotalCredit = 45m },
            });
        var mappings = new Mock<IPostingAccountMappingRepository>();
        mappings.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<PostingAccountMapping>());

        var result = await new GetLoanReconciliationQueryHandler(loans.Object, accounts.Object, lines.Object, mappings.Object)
            .Handle(new GetLoanReconciliationQuery { AsOfDate = DateTime.UtcNow.Date.AddYears(1) }, default);

        result.IsReconciled.Should().BeTrue();
        result.InterestAccountNumber.Should().Be("335");
        result.InterestLedger.Should().Be(45m);
        result.InterestBook.Should().Be(45m);
        result.InterestReconciled.Should().BeTrue();
    }
}

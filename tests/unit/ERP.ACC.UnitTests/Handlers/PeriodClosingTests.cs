using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Queries;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Handlers;

/// <summary>ROADMAP P1-2: closing a fiscal period writes the 911 -> 421 transfer, locks the period and can be undone by reopening it.</summary>
public class PeriodClosingTests
{
    private readonly List<Account> _accounts = new();
    private readonly List<PostingAccountMapping> _mappings = new();
    private readonly List<FiscalPeriod> _periods = new();
    private readonly List<JournalEntry> _entries = new();
    private readonly List<AccountBalanceSummary> _balances = new();
    private readonly List<PostingRequest> _posted = new();
    private readonly Mock<IAccountingUnitOfWork> _uow = new();
    private readonly Mock<IJournalPostingService> _posting = new();
    private readonly FiscalPeriod _october;

    private readonly Account _cash, _revenue, _returns, _cogs, _salary, _summary, _retained;

    public PeriodClosingTests()
    {
        _cash = Add("111", AccountType.Asset, NormalBalance.Debit);
        _revenue = Add("5111", AccountType.Revenue, NormalBalance.Credit);
        _returns = Add("5212", AccountType.Revenue, NormalBalance.Debit);
        _cogs = Add("632", AccountType.Expense, NormalBalance.Debit);
        _salary = Add("6421", AccountType.Expense, NormalBalance.Debit);
        _summary = Add("911", AccountType.Equity, NormalBalance.Credit);
        _retained = Add("4212", AccountType.Equity, NormalBalance.Credit);
        _mappings.Add(PostingAccountMapping.Create(Guid.NewGuid(), PostingKeys.IncomeSummary, _summary.Id));
        _mappings.Add(PostingAccountMapping.Create(Guid.NewGuid(), PostingKeys.RetainedEarnings, _retained.Id));

        _october = FiscalPeriod.Create("October 2026", new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));
        _periods.Add(_october);

        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _accounts.ToList());
        accounts.Setup(r => r.GetParentAccountIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HashSet<Guid>());
        var periods = new Mock<IFiscalPeriodRepository>();
        periods.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _periods.FirstOrDefault(p => p.Id == id));
        periods.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _periods.ToList());
        var entries = new Mock<IJournalEntryRepository>();
        entries.Setup(r => r.GetByFiscalPeriodIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _entries.ToList());
        entries.Setup(r => r.GetBySourceKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => _entries.FirstOrDefault(e => e.SourceKey == key));
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(r => r.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _balances.ToList());
        var mappings = new Mock<IPostingAccountMappingRepository>();
        mappings.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _mappings.ToList());

        _uow.SetupGet(u => u.Accounts).Returns(accounts.Object);
        _uow.SetupGet(u => u.FiscalPeriods).Returns(periods.Object);
        _uow.SetupGet(u => u.JournalEntries).Returns(entries.Object);
        _uow.SetupGet(u => u.JournalLines).Returns(lines.Object);
        _uow.SetupGet(u => u.PostingMappings).Returns(mappings.Object);
        var policies = new Mock<IClosingPolicyRepository>(); // no policy = Monthly
        policies.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((ClosingPolicy?)null);
        _uow.SetupGet(u => u.ClosingPolicies).Returns(policies.Object);

        _posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostingRequest, CancellationToken>((r, _) => _posted.Add(r))
            .ReturnsAsync((PostingRequest r, CancellationToken _) => JournalEntry.CreatePosted("JE-C", _october.Id, r.Date, r.Description, r.SourceType, r.SourceId,
                new[] { new JournalLineDraft(_cash.Id, 1m, 0m), new JournalLineDraft(_summary.Id, 0m, 1m) }));
    }

    private Account Add(string number, AccountType type, NormalBalance normal)
    {
        var account = Account.Create(number, "Account " + number, (int)type, (int)normal);
        _accounts.Add(account);
        return account;
    }

    private void Balance(Account account, decimal debit, decimal credit) =>
        _balances.Add(new AccountBalanceSummary { AccountId = account.Id, TotalDebit = debit, TotalCredit = credit });

    private CloseFiscalPeriodCommandHandler Close() => new(_uow.Object, _posting.Object);

    [Fact]
    public async Task A_profit_closes_revenue_and_expenses_into_the_income_summary_then_into_retained_earnings()
    {
        // debits 700 + 100 + 500 + 200 = 1500, credits 1000 + 500 (capital): the period balances
        Balance(_cash, 700m, 0m);
        Balance(_revenue, 0m, 1000m);
        Balance(_returns, 100m, 0m);   // contra revenue: debit balance
        Balance(_cogs, 500m, 0m);
        Balance(_salary, 200m, 0m);
        Balance(_retained, 0m, 500m);

        var dto = await Close().Handle(new CloseFiscalPeriodCommand { FiscalPeriodId = _october.Id }, default);

        dto.Should().NotBeNull();
        _october.Status.Should().Be((int)FiscalPeriodStatus.Closed);
        var request = _posted.Should().ContainSingle().Subject;
        request.SourceType.Should().Be(JournalEntry.PeriodClosingType);
        request.SourceId.Should().Be(_october.Id);
        request.Date.Should().Be(new DateTime(2026, 10, 31));
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));

        // each P&L account is cleared on its own side
        Line(request, _revenue).Should().Be((1000m, 0m));
        Line(request, _returns).Should().Be((0m, 100m));
        Line(request, _cogs).Should().Be((0m, 500m));
        Line(request, _salary).Should().Be((0m, 200m));
        // profit = 1000 - 100 - 500 - 200 = 200: credited to the summary (911), then moved to retained earnings (421)
        request.Lines.Where(l => l.PostingKey == PostingKeys.IncomeSummary).Select(l => (l.Debit, l.Credit)).Should().BeEquivalentTo(new[] { (0m, 200m), (200m, 0m) });
        request.Lines.Single(l => l.PostingKey == PostingKeys.RetainedEarnings).Credit.Should().Be(200m);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static (decimal Dr, decimal Cr) Line(PostingRequest request, Account account)
    {
        var line = request.Lines.Single(l => l.AccountId == account.Id);
        return (line.Debit, line.Credit);
    }

    [Fact]
    public async Task A_loss_is_debited_to_retained_earnings()
    {
        Balance(_cash, 0m, 300m);
        Balance(_revenue, 0m, 100m);
        Balance(_cogs, 400m, 0m);

        await Close().Handle(new CloseFiscalPeriodCommand { FiscalPeriodId = _october.Id }, default);

        var request = _posted.Single();
        request.Lines.Single(l => l.PostingKey == PostingKeys.RetainedEarnings).Debit.Should().Be(300m);
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public async Task One_account_for_both_keys_skips_the_transfer_lines()
    {
        _mappings.Clear();
        _mappings.Add(PostingAccountMapping.Create(Guid.NewGuid(), PostingKeys.IncomeSummary, _retained.Id));
        _mappings.Add(PostingAccountMapping.Create(Guid.NewGuid(), PostingKeys.RetainedEarnings, _retained.Id));
        Balance(_cash, 100m, 0m);
        Balance(_revenue, 0m, 100m);

        await Close().Handle(new CloseFiscalPeriodCommand { FiscalPeriodId = _october.Id }, default);

        _posted.Single().Lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_period_without_revenue_or_expense_is_closed_without_an_entry()
    {
        Balance(_cash, 100m, 0m);
        Balance(_summary, 0m, 100m);

        await Close().Handle(new CloseFiscalPeriodCommand { FiscalPeriodId = _october.Id }, default);

        _posted.Should().BeEmpty();
        _october.Status.Should().Be((int)FiscalPeriodStatus.Closed);
    }

    [Fact]
    public async Task Closing_can_skip_the_closing_entry_on_request()
    {
        Balance(_cash, 100m, 0m);
        Balance(_revenue, 0m, 100m);

        await Close().Handle(new CloseFiscalPeriodCommand { FiscalPeriodId = _october.Id, PostClosingEntries = false }, default);

        _posted.Should().BeEmpty();
        _october.Status.Should().Be((int)FiscalPeriodStatus.Closed);
    }

    [Fact]
    public async Task Closing_is_refused_for_an_earlier_open_period_a_draft_entry_an_unbalanced_book_or_a_closed_period()
    {
        var september = FiscalPeriod.Create("September 2026", new DateTime(2026, 9, 1), new DateTime(2026, 9, 30));
        _periods.Add(september);
        await FluentActions.Awaiting(() => Close().Handle(new CloseFiscalPeriodCommand { FiscalPeriodId = _october.Id }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*September 2026 first*");
        september.Close();

        var draft = JournalEntry.Create("JE-D", _october.Id, "draft");
        _entries.Add(draft);
        await FluentActions.Awaiting(() => Close().Handle(new CloseFiscalPeriodCommand { FiscalPeriodId = _october.Id }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*draft*");
        _entries.Clear();

        Balance(_cash, 100m, 0m);
        await FluentActions.Awaiting(() => Close().Handle(new CloseFiscalPeriodCommand { FiscalPeriodId = _october.Id }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*does not balance*");
        _balances.Clear();

        _october.Close();
        await FluentActions.Awaiting(() => Close().Handle(new CloseFiscalPeriodCommand { FiscalPeriodId = _october.Id }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*already closed*");
        _posted.Should().BeEmpty();
    }

    // ---- reopen ----

    private JournalEntry ClosingEntry()
    {
        var entry = JournalEntry.CreatePosted("JE-C", _october.Id, new DateTime(2026, 10, 31), "closing", JournalEntry.PeriodClosingType, _october.Id,
            new[] { new JournalLineDraft(_revenue.Id, 100m, 0m), new JournalLineDraft(_summary.Id, 0m, 100m) });
        _entries.Add(entry);
        return entry;
    }

    [Fact]
    public async Task Reopening_voids_the_closing_entry_frees_its_key_and_opens_the_period()
    {
        _october.Close();
        var closing = ClosingEntry();
        var originalKey = closing.SourceKey;

        await new ReopenFiscalPeriodCommandHandler(_uow.Object)
            .Handle(new ReopenFiscalPeriodCommand { FiscalPeriodId = _october.Id, Reason = "late invoice" }, default);

        _october.Status.Should().Be((int)FiscalPeriodStatus.Open);
        closing.Status.Should().Be((int)JournalStatus.Voided);
        closing.SourceKey.Should().StartWith(originalKey).And.NotBe(originalKey);
        closing.VoidReason.Should().Contain("late invoice");
    }

    [Fact]
    public async Task Reopening_needs_a_closed_period_a_reason_and_no_later_closed_period()
    {
        var november = FiscalPeriod.Create("November 2026", new DateTime(2026, 11, 1), new DateTime(2026, 11, 30));
        _periods.Add(november);
        var handler = new ReopenFiscalPeriodCommandHandler(_uow.Object);

        await FluentActions.Awaiting(() => handler.Handle(new ReopenFiscalPeriodCommand { FiscalPeriodId = _october.Id, Reason = "x" }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*not closed*");

        _october.Close();
        november.Close();
        await FluentActions.Awaiting(() => handler.Handle(new ReopenFiscalPeriodCommand { FiscalPeriodId = _october.Id, Reason = "x" }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*November 2026*");
        await FluentActions.Awaiting(() => handler.Handle(new ReopenFiscalPeriodCommand { FiscalPeriodId = november.Id, Reason = " " }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*reason*");
    }

    // ---- reports ----

    [Fact]
    public async Task The_balance_sheet_includes_the_unclosed_result_so_assets_equal_liabilities_plus_equity()
    {
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(r => r.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountBalanceSummary>
            {
                new() { AccountId = _cash.Id, TotalDebit = 1000m },
                new() { AccountId = _retained.Id, TotalCredit = 300m },
                new() { AccountId = _revenue.Id, TotalCredit = 800m },
                new() { AccountId = _cogs.Id, TotalDebit = 100m },
            });
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _accounts.ToList());

        var sheet = await new GetBalanceSheetQueryHandler(accounts.Object, lines.Object)
            .Handle(new GetBalanceSheetQuery { AsOfDate = new DateTime(2026, 10, 31) }, default);

        sheet.TotalAssets.Should().Be(1000m);
        sheet.TotalEquity.Should().Be(300m + 700m);
        sheet.EquityLines.Should().Contain(l => l.AccountName.Contains("not yet closed") && l.Amount == 700m);
        sheet.TotalAssets.Should().Be(sheet.TotalLiabilities + sheet.TotalEquity);
    }

    [Fact]
    public async Task The_profit_and_loss_leaves_out_the_closing_entries()
    {
        string? excluded = null;
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(r => r.GetActivityBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime?, DateTime?, string?, CancellationToken>((_, _, x, _) => excluded = x)
            .ReturnsAsync(new List<AccountBalanceSummary>
            {
                new() { AccountId = _revenue.Id, TotalCredit = 800m },
                new() { AccountId = _returns.Id, TotalDebit = 100m },
                new() { AccountId = _cogs.Id, TotalDebit = 300m },
            });
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _accounts.ToList());

        var pnl = await new GetProfitAndLossQueryHandler(accounts.Object, lines.Object)
            .Handle(new GetProfitAndLossQuery { FromDate = new DateTime(2026, 10, 1), ToDate = new DateTime(2026, 10, 31) }, default);

        excluded.Should().Be(JournalEntry.PeriodClosingType);
        pnl.TotalRevenue.Should().Be(800m - 100m);
        pnl.TotalExpenses.Should().Be(300m);
        pnl.NetIncome.Should().Be(400m);
    }
}

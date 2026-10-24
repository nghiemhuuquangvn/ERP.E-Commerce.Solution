using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Queries;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-2b: the account ledger and the trial balance with opening / period / closing columns.</summary>
public class LedgerAndTrialBalanceTests
{
    private readonly List<Account> _accounts = new();
    private readonly Account _cash, _cashVnd, _cashFx, _receivable, _revenue, _capital;

    // journal lines: (entry number, date, account, debit, credit)
    private readonly List<(string Entry, DateTime Date, Account Account, decimal Debit, decimal Credit)> _book = new();

    public LedgerAndTrialBalanceTests()
    {
        _cash = Add("111", AccountType.Asset, NormalBalance.Debit);
        _cashVnd = Add("1111", AccountType.Asset, NormalBalance.Debit, _cash);
        _cashFx = Add("1112", AccountType.Asset, NormalBalance.Debit, _cash);
        _receivable = Add("131", AccountType.Asset, NormalBalance.Debit);
        _revenue = Add("5111", AccountType.Revenue, NormalBalance.Credit);
        _capital = Add("4111", AccountType.Equity, NormalBalance.Credit);

        Post("JE-1", new DateTime(2026, 9, 10), (_cashVnd, 1000m, 0m), (_capital, 0m, 1000m));            // capital paid in (September)
        Post("JE-2", new DateTime(2026, 10, 5), (_receivable, 400m, 0m), (_revenue, 0m, 400m));           // sale on credit
        Post("JE-3", new DateTime(2026, 10, 12), (_cashVnd, 300m, 0m), (_receivable, 0m, 300m));          // part paid in cash
        Post("JE-4", new DateTime(2026, 10, 20), (_cashFx, 50m, 0m), (_cashVnd, 0m, 50m));                // transfer between sub-accounts
    }

    private Account Add(string number, AccountType type, NormalBalance normal, Account? parent = null)
    {
        var account = Account.Create(number, "Account " + number, (int)type, (int)normal, parent);
        _accounts.Add(account);
        return account;
    }

    private void Post(string entry, DateTime date, params (Account Account, decimal Debit, decimal Credit)[] lines)
    {
        foreach (var l in lines)
            _book.Add((entry, date, l.Account, l.Debit, l.Credit));
    }

    private IJournalLineRepository Lines()
    {
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(r => r.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime? from, DateTime? to, Guid? _, CancellationToken _) => _book
                .Where(x => (!from.HasValue || x.Date >= from.Value.Date) && (!to.HasValue || x.Date <= to.Value.Date))
                .GroupBy(x => x.Account.Id)
                .Select(g => new AccountBalanceSummary { AccountId = g.Key, TotalDebit = g.Sum(x => x.Debit), TotalCredit = g.Sum(x => x.Credit) })
                .ToList());
        lines.Setup(r => r.GetLedgerLinesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, DateTime? from, DateTime? to, CancellationToken _) => _book
                .Where(x => ids.Contains(x.Account.Id) && (!from.HasValue || x.Date >= from.Value.Date) && (!to.HasValue || x.Date <= to.Value.Date))
                .OrderBy(x => x.Date).ThenBy(x => x.Entry)
                .Select(x => new LedgerRow
                {
                    EntryId = EntryId(x.Entry), EntryNumber = x.Entry, EntryDate = x.Date, EntryDescription = "entry " + x.Entry,
                    AccountId = x.Account.Id, Debit = x.Debit, Credit = x.Credit, Status = (int)JournalStatus.Posted
                }).ToList());
        lines.Setup(r => r.GetEntryAccountsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) => _book
                .Where(x => ids.Contains(EntryId(x.Entry))).Select(x => new EntryAccountRef(EntryId(x.Entry), x.Account.Id)).ToList());
        return lines.Object;
    }

    private static Guid EntryId(string entry) => new(entry.GetHashCode() == 0 ? new byte[16] : BitConverter.GetBytes(entry.Aggregate(17, (h, c) => h * 31 + c)).Concat(new byte[12]).ToArray());

    private IAccountRepository Accounts()
    {
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _accounts.ToList());
        return accounts.Object;
    }

    // ---- ledger ----

    [Fact]
    public async Task The_ledger_of_an_account_has_opening_balance_running_balance_and_counter_accounts()
    {
        var ledger = await new GetAccountLedgerQueryHandler(Accounts(), Lines()).Handle(new GetAccountLedgerQuery
        {
            AccountId = _receivable.Id, FromDate = new DateTime(2026, 10, 1), ToDate = new DateTime(2026, 10, 31)
        }, default);

        ledger.OpeningBalance.Should().Be(0m);
        ledger.Lines.Select(l => (l.EntryNumber, l.Debit, l.Credit, l.Balance)).Should().Equal(("JE-2", 400m, 0m, 400m), ("JE-3", 0m, 300m, 100m));
        ledger.Lines[0].CounterAccounts.Should().Be("5111");
        ledger.Lines[1].CounterAccounts.Should().Be("1111");
        ledger.TotalDebit.Should().Be(400m);
        ledger.TotalCredit.Should().Be(300m);
        ledger.ClosingBalance.Should().Be(100m);
    }

    [Fact]
    public async Task The_ledger_starts_from_the_balance_before_the_range()
    {
        var ledger = await new GetAccountLedgerQueryHandler(Accounts(), Lines()).Handle(new GetAccountLedgerQuery
        {
            AccountId = _cashVnd.Id, FromDate = new DateTime(2026, 10, 1), ToDate = new DateTime(2026, 10, 31)
        }, default);

        ledger.OpeningBalance.Should().Be(1000m);                    // the September capital
        ledger.Lines.Select(l => l.Balance).Should().Equal(1300m, 1250m);
        ledger.ClosingBalance.Should().Be(1250m);
    }

    [Fact]
    public async Task A_credit_normal_account_shows_its_balance_on_the_credit_side()
    {
        var ledger = await new GetAccountLedgerQueryHandler(Accounts(), Lines()).Handle(new GetAccountLedgerQuery { AccountId = _capital.Id }, default);

        ledger.OpeningBalance.Should().Be(0m);
        ledger.ClosingBalance.Should().Be(1000m);
        ledger.Lines.Single().Balance.Should().Be(1000m);
    }

    [Fact]
    public async Task A_parent_includes_its_sub_accounts_and_a_transfer_between_them_nets_out()
    {
        var withSubs = await new GetAccountLedgerQueryHandler(Accounts(), Lines()).Handle(new GetAccountLedgerQuery { AccountId = _cash.Id }, default);
        var alone = await new GetAccountLedgerQueryHandler(Accounts(), Lines()).Handle(new GetAccountLedgerQuery { AccountId = _cash.Id, IncludeSubAccounts = false }, default);

        withSubs.IncludesSubAccounts.Should().BeTrue();
        withSubs.ClosingBalance.Should().Be(1300m);                 // 1000 + 300 + (50 - 50)
        withSubs.Lines.Should().HaveCount(4);
        alone.Lines.Should().BeEmpty();
        alone.ClosingBalance.Should().Be(0m);
    }

    [Fact]
    public async Task The_ledger_is_paged_but_the_running_balance_covers_all_rows()
    {
        var page2 = await new GetAccountLedgerQueryHandler(Accounts(), Lines()).Handle(new GetAccountLedgerQuery
        {
            AccountId = _cash.Id, PageNumber = 2, PageSize = 3
        }, default);

        page2.TotalRows.Should().Be(4);
        page2.Lines.Should().ContainSingle().Which.Balance.Should().Be(1300m);
    }

    [Fact]
    public async Task An_unknown_account_and_an_inverted_range_are_refused()
    {
        var handler = new GetAccountLedgerQueryHandler(Accounts(), Lines());

        await FluentActions.Awaiting(() => handler.Handle(new GetAccountLedgerQuery { AccountId = Guid.NewGuid() }, default)).Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Awaiting(() => handler.Handle(new GetAccountLedgerQuery
            { AccountId = _cash.Id, FromDate = new DateTime(2026, 11, 1), ToDate = new DateTime(2026, 10, 1) }, default)).Should().ThrowAsync<BusinessRuleException>();
    }

    // ---- trial balance ----

    private Task<TrialBalanceDto> TrialBalance(bool rollUp, bool hideZero = true) =>
        new GetTrialBalanceFullQueryHandler(Accounts(), Lines(), new Mock<IFiscalPeriodRepository>().Object).Handle(new GetTrialBalanceFullQuery
        {
            FromDate = new DateTime(2026, 10, 1), ToDate = new DateTime(2026, 10, 31), RollUp = rollUp, HideZeroBalance = hideZero
        }, default);

    [Fact]
    public async Task The_trial_balance_has_opening_period_and_closing_columns_that_balance()
    {
        var tb = await TrialBalance(rollUp: false);

        var receivable = tb.Lines.Single(l => l.AccountNumber == "131");
        (receivable.OpeningDebit, receivable.PeriodDebit, receivable.PeriodCredit, receivable.ClosingDebit).Should().Be((0m, 400m, 300m, 100m));
        var cashVnd = tb.Lines.Single(l => l.AccountNumber == "1111");
        (cashVnd.OpeningDebit, cashVnd.ClosingDebit).Should().Be((1000m, 1250m));
        var capital = tb.Lines.Single(l => l.AccountNumber == "4111");
        (capital.OpeningCredit, capital.ClosingCredit, capital.ClosingDebit).Should().Be((1000m, 1000m, 0m));

        tb.Totals.IsBalanced.Should().BeTrue();
        tb.Totals.OpeningDebit.Should().Be(tb.Totals.OpeningCredit).And.Be(1000m);
        tb.Totals.PeriodDebit.Should().Be(tb.Totals.PeriodCredit);
        tb.Totals.ClosingDebit.Should().Be(tb.Totals.ClosingCredit);
    }

    [Fact]
    public async Task With_roll_up_a_parent_carries_its_sub_accounts_and_the_totals_do_not_count_twice()
    {
        var flat = await TrialBalance(rollUp: false);
        var rolled = await TrialBalance(rollUp: true);

        var parent = rolled.Lines.Single(l => l.AccountNumber == "111");
        parent.IsParent.Should().BeTrue();
        (parent.OpeningDebit, parent.PeriodDebit, parent.PeriodCredit, parent.ClosingDebit).Should().Be((1000m, 350m, 50m, 1300m));
        rolled.Lines.Single(l => l.AccountNumber == "1111").Level.Should().Be(2);
        rolled.Totals.Should().BeEquivalentTo(flat.Totals);
    }

    [Fact]
    public async Task Zero_accounts_are_hidden_unless_asked_for()
    {
        (await TrialBalance(rollUp: false)).Lines.Should().NotContain(l => l.AccountNumber == "5111" && l.PeriodCredit == 0m);
        var all = await TrialBalance(rollUp: false, hideZero: false);
        all.Lines.Should().HaveCount(_accounts.Count);
    }

    [Fact]
    public async Task Without_a_start_date_everything_is_period_activity_and_the_opening_is_zero()
    {
        var tb = await new GetTrialBalanceFullQueryHandler(Accounts(), Lines(), new Mock<IFiscalPeriodRepository>().Object)
            .Handle(new GetTrialBalanceFullQuery { ToDate = new DateTime(2026, 10, 31), RollUp = false }, default);

        tb.Totals.OpeningDebit.Should().Be(0m);
        tb.Totals.PeriodDebit.Should().Be(tb.Totals.PeriodCredit);
        tb.Lines.Single(l => l.AccountNumber == "4111").ClosingCredit.Should().Be(1000m);
    }
}

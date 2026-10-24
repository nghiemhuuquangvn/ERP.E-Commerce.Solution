using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Handlers;

/// <summary>ROADMAP P1-2d: the closing policy. Monthly keeps one closing entry per period; YearEnd only locks until the last period of the fiscal year.</summary>
public class YearEndClosingTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private readonly List<Account> _accounts = new();
    private readonly List<PostingAccountMapping> _mappings = new();
    private readonly List<FiscalPeriod> _periods = new();
    private readonly List<PostingRequest> _posted = new();
    private readonly List<(DateTime? To, Guid? Period)> _balanceCalls = new();
    private readonly Mock<IAccountingUnitOfWork> _uow = new();
    private readonly Mock<IJournalPostingService> _posting = new();
    private readonly Mock<IClosingPolicyRepository> _policies = new();
    private ClosingPolicy? _policy;
    private readonly Account _revenue, _cogs, _summary, _retained;
    private readonly List<AccountBalanceSummary> _periodBalances = new();
    private readonly List<AccountBalanceSummary> _cumulativeBalances = new();

    public YearEndClosingTests()
    {
        _revenue = Add("5111", AccountType.Revenue, NormalBalance.Credit);
        _cogs = Add("632", AccountType.Expense, NormalBalance.Debit);
        _summary = Add("911", AccountType.Equity, NormalBalance.Credit);
        _retained = Add("4212", AccountType.Equity, NormalBalance.Credit);
        _mappings.Add(PostingAccountMapping.Create(Tenant, PostingKeys.IncomeSummary, _summary.Id));
        _mappings.Add(PostingAccountMapping.Create(Tenant, PostingKeys.RetainedEarnings, _retained.Id));

        for (var month = 1; month <= 12; month++)
            _periods.Add(FiscalPeriod.Create($"{month:00}/2026", new DateTime(2026, month, 1), new DateTime(2026, month, DateTime.DaysInMonth(2026, month))));

        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _accounts.ToList());
        accounts.Setup(r => r.GetParentAccountIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HashSet<Guid>());
        var periods = new Mock<IFiscalPeriodRepository>();
        periods.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _periods.FirstOrDefault(p => p.Id == id));
        periods.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _periods.ToList());
        var entries = new Mock<IJournalEntryRepository>();
        entries.Setup(r => r.GetByFiscalPeriodIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<JournalEntry>());
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(r => r.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime?, DateTime?, Guid?, CancellationToken>((_, to, period, _) => _balanceCalls.Add((to, period)))
            .ReturnsAsync((DateTime? _, DateTime? _, Guid? period, CancellationToken _) => (period.HasValue ? _periodBalances : _cumulativeBalances).ToList());
        var mappings = new Mock<IPostingAccountMappingRepository>();
        mappings.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _mappings.ToList());
        _policies.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _policy);
        _policies.Setup(r => r.AddAsync(It.IsAny<ClosingPolicy>(), It.IsAny<CancellationToken>()))
            .Callback<ClosingPolicy, CancellationToken>((p, _) => _policy = p).Returns(Task.CompletedTask);

        _uow.SetupGet(u => u.Accounts).Returns(accounts.Object);
        _uow.SetupGet(u => u.FiscalPeriods).Returns(periods.Object);
        _uow.SetupGet(u => u.JournalEntries).Returns(entries.Object);
        _uow.SetupGet(u => u.JournalLines).Returns(lines.Object);
        _uow.SetupGet(u => u.PostingMappings).Returns(mappings.Object);
        _uow.SetupGet(u => u.ClosingPolicies).Returns(_policies.Object);

        _posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostingRequest, CancellationToken>((r, _) => _posted.Add(r))
            .ReturnsAsync((PostingRequest r, CancellationToken _) => JournalEntry.CreatePosted("JE-C", _periods[0].Id, r.Date, r.Description, r.SourceType, r.SourceId,
                new[] { new JournalLineDraft(_revenue.Id, 1m, 0m), new JournalLineDraft(_summary.Id, 0m, 1m) }));
    }

    private Account Add(string number, AccountType type, NormalBalance normal)
    {
        var account = Account.Create(number, "Account " + number, (int)type, (int)normal);
        _accounts.Add(account);
        return account;
    }

    private FiscalPeriod Month(int month) => _periods[month - 1];

    private CloseFiscalPeriodCommandHandler Close() => new(_uow.Object, _posting.Object);

    private Task Close(int month) => Close().Handle(new CloseFiscalPeriodCommand { FiscalPeriodId = Month(month).Id }, default);

    private void YearEnd(int endMonth = 12) => _policy = ClosingPolicy.Create(Tenant, ClosingMode.YearEnd, endMonth);

    private void Year()
    {
        // revenue 1200 and cost 700 over the year; every period balances on its own
        _periodBalances.Add(new AccountBalanceSummary { AccountId = _revenue.Id, TotalCredit = 100m });
        _periodBalances.Add(new AccountBalanceSummary { AccountId = _cogs.Id, TotalDebit = 100m });
        _cumulativeBalances.Add(new AccountBalanceSummary { AccountId = _revenue.Id, TotalCredit = 1200m });
        _cumulativeBalances.Add(new AccountBalanceSummary { AccountId = _cogs.Id, TotalDebit = 700m });
    }

    [Fact]
    public async Task Monthly_is_the_default_and_every_period_writes_its_entry_from_its_own_balances()
    {
        Year();

        await Close(1);

        var request = _posted.Should().ContainSingle().Subject;
        request.Lines.Single(l => l.AccountId == _revenue.Id).Debit.Should().Be(100m); // the period's 100, not the year's 1200
        _balanceCalls.Should().OnlyContain(c => c.Period.HasValue);
    }

    [Fact]
    public async Task In_year_end_mode_a_month_only_locks_and_writes_no_entry()
    {
        YearEnd();
        Year();

        await Close(1);
        await Close(2);

        _posted.Should().BeEmpty();
        Month(1).Status.Should().Be((int)FiscalPeriodStatus.Closed);
        Month(2).Status.Should().Be((int)FiscalPeriodStatus.Closed);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task The_last_period_of_the_year_closes_everything_not_yet_closed_from_the_cumulative_balances()
    {
        YearEnd();
        Year();
        for (var m = 1; m <= 11; m++)
            await Close(m);

        await Close(12);

        var request = _posted.Should().ContainSingle().Subject;
        request.SourceType.Should().Be(JournalEntry.PeriodClosingType);
        request.SourceId.Should().Be(Month(12).Id);
        request.Date.Should().Be(new DateTime(2026, 12, 31));
        request.Description.Should().StartWith("Year-end closing");
        request.Lines.Single(l => l.AccountId == _revenue.Id).Debit.Should().Be(1200m);
        request.Lines.Single(l => l.AccountId == _cogs.Id).Credit.Should().Be(700m);
        request.Lines.Where(l => l.PostingKey == PostingKeys.RetainedEarnings).Sum(l => l.Credit).Should().Be(500m); // profit 1200 - 700
        _balanceCalls.Should().Contain(c => !c.Period.HasValue && c.To == new DateTime(2026, 12, 31));
    }

    [Fact]
    public async Task A_fiscal_year_ending_in_another_month_closes_there()
    {
        YearEnd(endMonth: 6);
        Year();
        for (var m = 1; m <= 5; m++)
            await Close(m);
        _posted.Should().BeEmpty();

        await Close(6);

        _posted.Should().ContainSingle().Which.Date.Should().Be(new DateTime(2026, 6, 30));
    }

    [Fact]
    public async Task Close_without_entries_still_writes_nothing_even_in_the_last_period()
    {
        YearEnd();
        Year();
        for (var m = 1; m <= 11; m++)
            await Close(m);

        await Close().Handle(new CloseFiscalPeriodCommand { FiscalPeriodId = Month(12).Id, PostClosingEntries = false }, default);

        _posted.Should().BeEmpty();
        Month(12).Status.Should().Be((int)FiscalPeriodStatus.Closed);
    }

    [Fact]
    public void Only_a_period_that_ends_on_the_last_day_of_the_year_end_month_ends_the_year()
    {
        ClosingPolicy.EndsFiscalYear(Month(12), 12).Should().BeTrue();
        ClosingPolicy.EndsFiscalYear(Month(11), 12).Should().BeFalse();
        ClosingPolicy.EndsFiscalYear(FiscalPeriod.Create("mid", new DateTime(2026, 12, 1), new DateTime(2026, 12, 15)), 12).Should().BeFalse();
        ClosingPolicy.EndsFiscalYear(FiscalPeriod.Create("Q4", new DateTime(2026, 10, 1), new DateTime(2026, 12, 31)), 12).Should().BeTrue();
    }

    // ---- the policy itself ----

    private UpdateClosingPolicyCommandHandler Update()
    {
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(t => t.CurrentTenantId).Returns(Tenant);
        return new UpdateClosingPolicyCommandHandler(_uow.Object, tenant.Object);
    }

    [Fact]
    public async Task A_tenant_with_no_policy_is_monthly_with_a_december_year_end_and_can_switch_before_any_period_is_closed()
    {
        var current = await new GetClosingPolicyQueryHandler(_policies.Object).Handle(new GetClosingPolicyQuery(), default);
        current.ClosingMode.Should().Be("Monthly");
        current.FiscalYearEndMonth.Should().Be(12);

        var updated = await Update().Handle(new UpdateClosingPolicyCommand { ClosingMode = "yearend", FiscalYearEndMonth = 12 }, default);

        updated.ClosingMode.Should().Be("YearEnd");
        _policy.Should().NotBeNull();
        _policy!.TenantId.Should().Be(Tenant);
    }

    [Theory]
    [InlineData("Weekly", 12)]
    [InlineData("YearEnd", 0)]
    [InlineData("YearEnd", 13)]
    [InlineData("", 12)]
    public async Task An_unknown_mode_or_month_is_refused(string mode, int month) =>
        await FluentActions.Awaiting(() => Update().Handle(new UpdateClosingPolicyCommand { ClosingMode = mode, FiscalYearEndMonth = month }, default))
            .Should().ThrowAsync<BusinessRuleException>();

    [Fact]
    public async Task The_mode_cannot_change_while_the_fiscal_year_is_partly_closed()
    {
        Year();
        await Close(1); // monthly: January is closed, the year is in progress

        await FluentActions.Awaiting(() => Update().Handle(new UpdateClosingPolicyCommand { ClosingMode = "YearEnd", FiscalYearEndMonth = 12 }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*in progress*");
        _policy.Should().BeNull();
    }

    [Fact]
    public async Task The_mode_can_change_once_the_year_is_closed()
    {
        Year();
        foreach (var p in _periods)
            p.Close();

        var updated = await Update().Handle(new UpdateClosingPolicyCommand { ClosingMode = "YearEnd", FiscalYearEndMonth = 12 }, default);

        updated.ClosingMode.Should().Be("YearEnd");
    }

    [Fact]
    public async Task Saving_the_same_policy_while_the_year_is_in_progress_is_allowed()
    {
        Year();
        await Close(1);

        var same = await Update().Handle(new UpdateClosingPolicyCommand { ClosingMode = "Monthly", FiscalYearEndMonth = 12 }, default);

        same.ClosingMode.Should().Be("Monthly");
    }
}

using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Handlers;

/// <summary>ROADMAP P1-2i: the off-balance register (class 0 accounts) stays outside the journal.</summary>
public class OffBalanceTests
{
    private readonly List<Account> _chart = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();
    private readonly List<OffBalanceEntry> _entries = new();
    private readonly List<FiscalPeriod> _periods = new();
    private readonly Mock<IAccountingUnitOfWork> _uow = new();
    private readonly Mock<IOffBalanceRepository> _repo = new();

    public OffBalanceTests()
    {
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _chart.ToList());
        accounts.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => _chart.FirstOrDefault(a => a.Id == id));
        var periods = new Mock<IFiscalPeriodRepository>();
        periods.Setup(r => r.GetContainingAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime d, CancellationToken _) => _periods.FirstOrDefault(p => p.StartDate <= d && p.EndDate >= d));
        _repo.Setup(r => r.AddAsync(It.IsAny<OffBalanceEntry>(), It.IsAny<CancellationToken>())).Callback<OffBalanceEntry, CancellationToken>((e, _) => _entries.Add(e)).Returns(Task.CompletedTask);
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => _entries.FirstOrDefault(e => e.Id == id));
        _repo.Setup(r => r.ListAsync(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? a, DateTime? f, DateTime? t, CancellationToken _) => _entries
                .Where(e => (!a.HasValue || e.AccountId == a) && (!f.HasValue || e.EntryDate >= f.Value.Date) && (!t.HasValue || e.EntryDate <= t.Value.Date)).ToList());
        _uow.SetupGet(u => u.Accounts).Returns(accounts.Object);
        _uow.SetupGet(u => u.FiscalPeriods).Returns(periods.Object);
        _uow.SetupGet(u => u.OffBalanceEntries).Returns(_repo.Object);
    }

    private Account A(string number) => _chart.Single(a => a.AccountNumber == number);

    private PostOffBalanceEntryCommandHandler Post() => new(_uow.Object);

    private Task<OffBalanceEntryDto> Add(string account, string direction, decimal amount, string date = "2026-10-05") =>
        Post().Handle(new PostOffBalanceEntryCommand { AccountId = A(account).Id, Direction = direction, Amount = amount, EntryDate = DateTime.Parse(date), Description = "test" }, default);

    private Task<OffBalanceReportDto> Report(string from = "2026-10-01", string to = "2026-10-31") =>
        new GetOffBalanceReportQueryHandler(_repo.Object, _uow.Object.Accounts).Handle(new GetOffBalanceReportQuery { FromDate = DateTime.Parse(from), ToDate = DateTime.Parse(to) }, default);

    // ---- the template and the account type ----

    [Fact]
    public void The_vietnamese_template_has_the_class_0_accounts_as_off_balance_accounts()
    {
        var offBalance = _chart.Where(a => a.AccountNumber.StartsWith('0')).ToList();

        offBalance.Select(a => a.AccountNumber).Should().Equal("001", "002", "003", "004", "007", "008");
        offBalance.Should().OnlyContain(a => a.AccountType == (int)AccountType.OffBalance && a.NormalBalance == (int)NormalBalance.Debit);
        _chart.Where(a => !a.AccountNumber.StartsWith('0')).Should().OnlyContain(a => a.AccountType != (int)AccountType.OffBalance);
    }

    [Fact]
    public async Task A_class_0_account_never_reaches_the_journal()
    {
        var parentIds = new HashSet<Guid>();

        var act = () => Task.Run(() => AccountRulesProxy.EnsurePostable(A("001"), parentIds));

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*off-balance*register*");
        AccountRulesProxy.EnsurePostable(A("1111"), parentIds); // an ordinary account is fine
    }

    // ---- the register ----

    [Fact]
    public async Task An_entry_needs_an_off_balance_account_a_positive_amount_and_a_direction()
    {
        await FluentActions.Awaiting(() => Add("1111", "In", 100m)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*not an off-balance account*");
        await FluentActions.Awaiting(() => Add("001", "In", 0m)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*greater than 0*");
        await FluentActions.Awaiting(() => Add("001", "Sideways", 10m)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*In or Out*");
        _entries.Should().BeEmpty();
    }

    [Fact]
    public async Task The_report_gives_opening_in_out_and_closing_per_account()
    {
        await Add("001", "In", 1000m, "2026-09-10");       // before the range
        await Add("001", "Out", 200m, "2026-09-20");
        await Add("001", "In", 300m, "2026-10-05");
        await Add("001", "Out", 100m, "2026-10-20");
        await Add("007", "In", 50m, "2026-10-07");
        await Add("002", "In", 999m, "2026-11-02");        // after the range: ignored

        var report = await Report();

        var l001 = report.Lines.Single(l => l.AccountNumber == "001");
        (l001.Opening, l001.In, l001.Out, l001.Closing).Should().Be((800m, 300m, 100m, 1000m));
        var l007 = report.Lines.Single(l => l.AccountNumber == "007");
        (l007.Opening, l007.In, l007.Out, l007.Closing).Should().Be((0m, 50m, 0m, 50m));
        report.Lines.Select(l => l.AccountNumber).Should().Equal("001", "007");   // 002 has nothing up to the end of the range
        report.Lines.Should().OnlyContain(l => !l.IsNegative);
    }

    [Fact]
    public async Task A_negative_closing_balance_is_flagged()
    {
        await Add("003", "In", 100m);
        await Add("003", "Out", 150m);

        var report = await Report();

        report.Lines.Single().IsNegative.Should().BeTrue();
        report.Lines.Single().Closing.Should().Be(-50m);
        report.Notes.Should().Contain(n => n.Contains("negative"));
    }

    [Fact]
    public async Task A_voided_entry_leaves_the_report_and_needs_a_reason_and_can_be_voided_once()
    {
        var entry = await Add("001", "In", 500m);
        var void_ = new VoidOffBalanceEntryCommandHandler(_uow.Object);

        await FluentActions.Awaiting(() => void_.Handle(new VoidOffBalanceEntryCommand { EntryId = entry.Id, Reason = " " }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*reason*");

        var voided = await void_.Handle(new VoidOffBalanceEntryCommand { EntryId = entry.Id, Reason = "typed twice" }, default);

        voided.Status.Should().Be("Voided");
        voided.VoidReason.Should().Be("typed twice");
        (await Report()).Lines.Should().BeEmpty();
        await FluentActions.Awaiting(() => void_.Handle(new VoidOffBalanceEntryCommand { EntryId = entry.Id, Reason = "again" }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*posted*");
    }

    [Fact]
    public async Task A_closed_fiscal_period_locks_the_register()
    {
        var october = FiscalPeriod.Create("October 2026", new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));
        _periods.Add(october);
        var open = await Add("001", "In", 100m, "2026-10-05");
        october.Close();

        await FluentActions.Awaiting(() => Add("001", "In", 10m, "2026-10-06")).Should().ThrowAsync<BusinessRuleException>().WithMessage("*closed*");
        await FluentActions.Awaiting(() => new VoidOffBalanceEntryCommandHandler(_uow.Object).Handle(new VoidOffBalanceEntryCommand { EntryId = open.Id, Reason = "x" }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*closed*");
        (await Add("001", "In", 10m, "2026-11-02")).Status.Should().Be("Posted"); // a date outside any period is fine
    }

    [Fact]
    public async Task The_list_filters_by_account_and_date()
    {
        await Add("001", "In", 1m, "2026-10-01");
        await Add("007", "In", 2m, "2026-10-02");

        var handler = new ListOffBalanceEntriesQueryHandler(_repo.Object, _uow.Object.Accounts);
        (await handler.Handle(new ListOffBalanceEntriesQuery(), default)).Should().HaveCount(2);
        var onlyFx = await handler.Handle(new ListOffBalanceEntriesQuery { AccountId = A("007").Id }, default);
        onlyFx.Should().ContainSingle().Which.AccountNumber.Should().Be("007");
    }

    [Fact]
    public async Task The_report_is_a_table_for_csv_and_excel_and_a_wrong_range_is_refused()
    {
        await Add("001", "In", 100m);
        var table = ReportTables.OffBalance(await Report());

        table.Rows[1].Should().Equal("Account", "Name", "Opening", "In", "Out", "Closing");
        table.Rows[2][0].Should().Be("001");
        await FluentActions.Awaiting(() => Report("2026-11-01", "2026-10-01")).Should().ThrowAsync<BusinessRuleException>();
    }
}

/// <summary>AccountRules is internal to the Application assembly; the test goes through the manual-entry rule it exposes.</summary>
internal static class AccountRulesProxy
{
    public static void EnsurePostable(Account account, HashSet<Guid> parents)
    {
        var rules = typeof(PostOffBalanceEntryCommand).Assembly.GetType("ERP.ACC.Application.Handlers.AccountRules")!;
        try
        {
            rules.GetMethod("EnsurePostable")!.Invoke(null, new object[] { account, parents });
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }
}

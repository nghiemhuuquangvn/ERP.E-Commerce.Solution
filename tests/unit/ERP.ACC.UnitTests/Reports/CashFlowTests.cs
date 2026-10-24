using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Queries;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-2b: the direct-method cash-flow statement, classified by the counter-account.</summary>
public class CashFlowTests
{
    private readonly List<Account> _accounts;
    private readonly Account _cash, _bank, _bank2, _receivable, _payable, _revenue, _rent, _equipment, _loan, _capital;
    private readonly List<(Guid Entry, DateTime Date, Account Account, decimal Debit, decimal Credit)> _book = new();

    public CashFlowTests()
    {
        // the Vietnamese template's accounts, so the shipped cash-flow defaults are what is tested
        _accounts = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();
        Account A(string number) => _accounts.Single(a => a.AccountNumber == number);
        _cash = A("1111"); _bank = A("1121"); _bank2 = A("1122"); _receivable = A("131"); _payable = A("331");
        _revenue = A("5111"); _rent = A("6427"); _equipment = A("211"); _loan = A("341"); _capital = A("4111");

        Entry(Sep, (_bank, 5000m, 0m), (_capital, 0m, 5000m));           // before the range: capital paid in in September
    }

    private static DateTime Day(int d) => new(2026, 10, d);
    private static DateTime Sep => new(2026, 9, 15);

    private Guid Entry(DateTime date, params (Account Account, decimal Debit, decimal Credit)[] lines)
    {
        var id = Guid.NewGuid();
        foreach (var l in lines)
            _book.Add((id, date, l.Account, l.Debit, l.Credit));
        return id;
    }

    private GetCashFlowQueryHandler Handler()
    {
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _accounts.ToList());

        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(r => r.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime? from, DateTime? to, Guid? _, CancellationToken _) => _book
                .Where(x => (!from.HasValue || x.Date >= from.Value.Date) && (!to.HasValue || x.Date <= to.Value.Date))
                .GroupBy(x => x.Account.Id)
                .Select(g => new AccountBalanceSummary { AccountId = g.Key, TotalDebit = g.Sum(x => x.Debit), TotalCredit = g.Sum(x => x.Credit) })
                .ToList());
        lines.Setup(r => r.GetCashEntryLinesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> cashIds, DateTime from, DateTime to, CancellationToken _) =>
            {
                var inRange = _book.Where(x => x.Date >= from.Date && x.Date <= to.Date).ToList();
                var entries = inRange.Where(x => cashIds.Contains(x.Account.Id)).Select(x => x.Entry).ToHashSet();
                return inRange.Where(x => entries.Contains(x.Entry))
                    .Select(x => new CashEntryLine { EntryId = x.Entry, AccountId = x.Account.Id, Debit = x.Debit, Credit = x.Credit }).ToList();
            });
        return new GetCashFlowQueryHandler(accounts.Object, lines.Object);
    }

    private Task<CashFlowStatementDto> Report() =>
        Handler().Handle(new GetCashFlowQuery { FromDate = new DateTime(2026, 10, 1), ToDate = new DateTime(2026, 10, 31) }, default);

    [Fact]
    public async Task A_sale_on_credit_moves_no_cash_until_it_is_paid_and_then_is_an_operating_inflow()
    {
        _book.Clear();
        Entry(Sep, (_bank, 1000m, 0m), (_capital, 0m, 1000m));
        Entry(Day(3), (_receivable, 600m, 0m), (_revenue, 0m, 600m));             // sale on credit: no cash
        Entry(Day(9), (_bank, 400m, 0m), (_receivable, 0m, 400m));                // part of it paid

        var cf = await Report();

        cf.Operating.TotalInflow.Should().Be(400m);
        cf.Operating.Lines.Should().ContainSingle().Which.AccountNumber.Should().Be("131");
        cf.Investing.Lines.Should().BeEmpty();
        cf.Financing.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task Each_kind_of_movement_lands_in_its_category_and_the_statement_reconciles_with_the_balance_sheet()
    {
        Entry(Day(2), (_bank, 2000m, 0m), (_loan, 0m, 2000m));                    // loan received: financing +2000
        Entry(Day(4), (_equipment, 1500m, 0m), (_bank, 0m, 1500m));               // equipment bought: investing -1500
        Entry(Day(6), (_rent, 300m, 0m), (_bank, 0m, 300m));                      // rent paid: operating -300
        Entry(Day(8), (_receivable, 900m, 0m), (_revenue, 0m, 900m));            // sale on credit (no cash)
        Entry(Day(10), (_bank, 900m, 0m), (_receivable, 0m, 900m));               // customer pays: operating +900
        Entry(Day(12), (_payable, 100m, 0m), (_bank, 0m, 100m));                  // supplier paid: operating -100

        var cf = await Report();

        cf.OpeningCash.Should().Be(5000m);
        cf.Operating.Net.Should().Be(900m - 300m - 100m);
        cf.Investing.Net.Should().Be(-1500m);
        cf.Financing.Net.Should().Be(2000m);
        cf.NetChange.Should().Be(500m - 1500m + 2000m);
        cf.ClosingCash.Should().Be(5000m + cf.NetChange);
        cf.IsReconciled.Should().BeTrue();
        cf.Difference.Should().Be(0m);
        cf.Financing.Lines.Single().AccountNumber.Should().Be("341");
        cf.Investing.Lines.Single().Outflow.Should().Be(1500m);
    }

    [Fact]
    public async Task A_transfer_between_cash_and_bank_accounts_is_not_a_cash_flow()
    {
        Entry(Day(5), (_cash, 400m, 0m), (_bank, 0m, 400m));
        Entry(Day(6), (_bank2, 100m, 0m), (_bank, 0m, 100m));

        var cf = await Report();

        cf.NetChange.Should().Be(0m);
        cf.Operating.Lines.Should().BeEmpty();
        cf.OpeningCash.Should().Be(5000m);
        cf.ClosingCash.Should().Be(5000m);
        cf.IsReconciled.Should().BeTrue();
    }

    [Fact]
    public async Task An_entry_with_several_counter_lines_attributes_each_line_to_its_own_category()
    {
        // 1000 received in cash: revenue 1100 against a 100 expense booked in the same entry (net 1000)
        Entry(Day(7), (_bank, 1000m, 0m), (_rent, 100m, 0m), (_revenue, 0m, 1100m));

        var cf = await Report();

        cf.Operating.Lines.Select(l => (l.AccountNumber, l.Inflow, l.Outflow)).Should().BeEquivalentTo(new[] { ("5111", 1100m, 0m), ("6427", 0m, 100m) });
        cf.Operating.Net.Should().Be(1000m);
        cf.IsReconciled.Should().BeTrue();
    }

    [Fact]
    public async Task A_reversal_cancels_the_original_in_the_same_statement()
    {
        Entry(Day(5), (_rent, 300m, 0m), (_bank, 0m, 300m));
        Entry(Day(6), (_rent, 0m, 300m), (_bank, 300m, 0m));                       // the reversal

        var cf = await Report();

        cf.Operating.Net.Should().Be(0m);
        cf.Operating.TotalInflow.Should().Be(300m);
        cf.Operating.TotalOutflow.Should().Be(300m);
        cf.IsReconciled.Should().BeTrue();
    }

    [Fact]
    public async Task Without_any_cash_account_the_statement_is_empty_and_an_inverted_range_is_refused()
    {
        foreach (var a in _accounts) a.SetCashFlow(false, a.CashFlowCategory);

        var cf = await Report();
        cf.CashAccountCount.Should().Be(0);
        cf.NetChange.Should().Be(0m);

        await FluentActions.Awaiting(() => Handler().Handle(new GetCashFlowQuery { FromDate = Day(20), ToDate = Day(1) }, default))
            .Should().ThrowAsync<BusinessRuleException>();
    }
}

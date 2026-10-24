using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-2d: the cash-flow statement B03-DN (direct method, by indicator code).</summary>
public class CashFlowStatementTests
{
    private readonly List<Account> _chart = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();

    private static IReportProfileRepository NoProfile() => new Mock<IReportProfileRepository>().Object;

    private Account A(string number) => _chart.Single(a => a.AccountNumber == number);

    private CashMovement M(string counter, decimal cash) => new(A(counter), cash);

    private static decimal Row(FinancialStatementDto s, string code) => s.Rows.Single(r => r.Code == code).Amount;

    private FinancialStatementDto Run(decimal opening, decimal closing, params CashMovement[] movements) =>
        CashFlowStatementEngine.Evaluate(CashFlowLayouts.Layout, new CashFlowColumn(movements, opening, closing), null);

    [Fact]
    public void Operating_receipts_and_payments_land_on_their_indicator_rows_with_outflows_negative()
    {
        var s = Run(1000m, 1000m + 900m - 300m - 200m - 50m - 30m,
            M("5111", 600m), M("131", 300m),   // received for sales (01)
            M("331", -300m),                   // paid to suppliers (02)
            M("334", -200m),                   // paid to employees (03)
            M("635", -50m),                    // interest (04)
            M("8211", -30m));                  // income tax (05)

        Row(s, "01").Should().Be(900m);
        Row(s, "02").Should().Be(-300m);
        Row(s, "03").Should().Be(-200m);
        Row(s, "04").Should().Be(-50m);
        Row(s, "05").Should().Be(-30m);
        Row(s, "20").Should().Be(320m);
        Row(s, "50").Should().Be(320m);
        Row(s, "60").Should().Be(1000m);
        Row(s, "70").Should().Be(1320m);
        s.IsBalanced.Should().BeTrue();
        s.Unmapped.Should().BeEmpty();
    }

    [Fact]
    public void An_operating_movement_that_no_row_names_falls_back_to_other_receipts_or_payments()
    {
        var s = Run(0m, 70m, M("711", 100m), M("642", 0m), M("3388", -30m));

        Row(s, "06").Should().Be(100m); // other income received
        Row(s, "07").Should().Be(-30m); // a payment on an account the layout does not name
        Row(s, "20").Should().Be(70m);
        s.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void Investing_and_financing_movements_use_their_own_sections()
    {
        var s = Run(0m, -500m + 80m + 20m + 1000m - 400m - 100m,
            M("211", -500m),  // buy equipment (21)
            M("128", 80m),    // loan repaid to us (24)
            M("515", 20m),    // interest received (27)
            M("411", 1000m),  // capital in (31)
            M("341", -400m),  // repay a bank loan (34)
            M("421", -100m)); // dividends (36)

        Row(s, "21").Should().Be(-500m);
        Row(s, "24").Should().Be(80m);
        Row(s, "27").Should().Be(20m);
        Row(s, "30").Should().Be(-400m);
        Row(s, "31").Should().Be(1000m);
        Row(s, "34").Should().Be(-400m);
        Row(s, "36").Should().Be(-100m);
        Row(s, "40").Should().Be(500m);
        Row(s, "50").Should().Be(100m);
        s.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void The_direction_decides_the_row_for_the_same_account()
    {
        var s = Run(0m, 300m - 100m, M("341", 300m), M("341", -100m));

        Row(s, "33").Should().Be(300m);
        Row(s, "34").Should().Be(-100m);
    }

    [Fact]
    public void A_movement_no_row_takes_is_reported_as_unmapped_and_breaks_the_reconciliation_check()
    {
        // 2291 (a provision) is investing/financing-neutral: operating by default, so use an investing account the layout leaves out
        var odd = A("242");
        odd.SetCashFlow(false, (int)ERP.ACC.Domain.Enums.CashFlowCategory.Investing);

        var s = Run(0m, 50m, new CashMovement(odd, 50m));

        s.Unmapped.Should().ContainSingle().Which.AccountNumber.Should().Be("242");
        s.IsBalanced.Should().BeFalse();
        s.Notes.Should().Contain(n => n.Contains("Closing cash (70)"));
    }

    [Fact]
    public void The_prior_column_is_computed_from_its_own_movements_and_cash()
    {
        var s = CashFlowStatementEngine.Evaluate(CashFlowLayouts.Layout,
            new CashFlowColumn(new[] { M("5111", 500m) }, 100m, 600m),
            new CashFlowColumn(new[] { M("5111", 90m) }, 10m, 100m));

        s.Rows.Single(r => r.Code == "01").PriorAmount.Should().Be(90m);
        s.Rows.Single(r => r.Code == "70").PriorAmount.Should().Be(100m);
        s.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void The_layout_is_consistent()
    {
        var rows = CashFlowLayouts.Layout.Rows;
        rows.Select(r => r.Code).Should().OnlyHaveUniqueItems();
        var codes = rows.Select(r => r.Code).ToHashSet();
        rows.SelectMany(r => r.Formula).Select(p => p.Code).Should().OnlyContain(c => codes.Contains(c));
        // a formula only refers to rows above it, so the statement can be read top to bottom
        foreach (var (row, i) in rows.Select((r, i) => (r, i)).Where(x => x.r.Formula.Count > 0))
            row.Formula.Select(p => rows.ToList().FindIndex(x => x.Code == p.Code)).Should().OnlyContain(j => j < i);
        CashFlowLayouts.Layout.Rows.Single(r => r.Code == "70").Formula.Select(p => p.Code).Should().BeEquivalentTo("50", "60", "61");
    }

    [Fact]
    public async Task The_handler_reads_the_cash_lines_and_balances_of_each_range_and_validates_the_dates()
    {
        var cash = A("1111");
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _chart.ToList());
        var lines = new Mock<IJournalLineRepository>();
        var from = new DateTime(2026, 10, 1);
        var to = new DateTime(2026, 10, 31);
        var entry = Guid.NewGuid();
        var transfer = Guid.NewGuid();
        lines.Setup(l => l.GetAccountBalancesAsync(null, from.AddDays(-1), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountBalanceSummary> { new() { AccountId = cash.Id, TotalDebit = 100m } });
        lines.Setup(l => l.GetAccountBalancesAsync(null, to, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountBalanceSummary> { new() { AccountId = cash.Id, TotalDebit = 700m } });
        lines.Setup(l => l.GetCashEntryLinesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CashEntryLine>
            {
                new() { EntryId = entry, AccountId = cash.Id, Debit = 600m },
                new() { EntryId = entry, AccountId = A("5111").Id, Credit = 600m },
                new() { EntryId = transfer, AccountId = cash.Id, Debit = 40m },   // cash <-> bank: no counter-line outside cash
                new() { EntryId = transfer, AccountId = A("1121").Id, Credit = 40m },
            });
        var handler = new GetCashFlowStatementQueryHandler(accounts.Object, lines.Object, NoProfile());

        var s = await handler.Handle(new GetCashFlowStatementQuery { FromDate = from, ToDate = to }, default);

        Row(s, "01").Should().Be(600m);
        Row(s, "60").Should().Be(100m);
        Row(s, "70").Should().Be(700m);
        s.IsBalanced.Should().BeTrue();
        s.FromDate.Should().Be(from);

        await FluentActions.Awaiting(() => handler.Handle(new GetCashFlowStatementQuery { FromDate = to, ToDate = from }, default))
            .Should().ThrowAsync<BusinessRuleException>();
        await FluentActions.Awaiting(() => handler.Handle(new GetCashFlowStatementQuery { FromDate = from, ToDate = to, CompareFromDate = from }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*both*");
    }
}

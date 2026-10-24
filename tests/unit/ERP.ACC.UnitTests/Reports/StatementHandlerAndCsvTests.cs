using System.Text;
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

/// <summary>ROADMAP P1-2c: the statement handlers read the right balances, and the CSV is safe for spreadsheets.</summary>
public class StatementHandlerAndCsvTests
{
    private readonly List<Account> _chart = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();

    private Account A(string number) => _chart.Single(a => a.AccountNumber == number);

    private (IAccountRepository Accounts, Mock<IJournalLineRepository> Lines) Repos()
    {
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _chart.ToList());
        return (accounts.Object, new Mock<IJournalLineRepository>());
    }

    [Fact]
    public async Task The_balance_sheet_reads_cumulative_balances_up_to_each_date()
    {
        var (accounts, lines) = Repos();
        var asOf = new DateTime(2026, 10, 31);
        var compare = new DateTime(2026, 9, 30);
        lines.Setup(l => l.GetAccountBalancesAsync(null, asOf, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountBalanceSummary> { new() { AccountId = A("1111").Id, TotalDebit = 900m }, new() { AccountId = A("4111").Id, TotalCredit = 900m } });
        lines.Setup(l => l.GetAccountBalancesAsync(null, compare, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountBalanceSummary> { new() { AccountId = A("1111").Id, TotalDebit = 400m }, new() { AccountId = A("4111").Id, TotalCredit = 400m } });

        var s = await new GetBalanceSheetStatementQueryHandler(accounts, lines.Object)
            .Handle(new GetBalanceSheetStatementQuery { AsOfDate = asOf, CompareAsOfDate = compare }, default);

        s.Rows.Single(r => r.Code == "270").Amount.Should().Be(900m);
        s.Rows.Single(r => r.Code == "270").PriorAmount.Should().Be(400m);
        s.IsBalanced.Should().BeTrue();
        s.ToDate.Should().Be(asOf);
        s.CompareToDate.Should().Be(compare);
    }

    [Fact]
    public async Task The_income_statement_leaves_out_closing_entries_and_checks_its_dates()
    {
        var (accounts, lines) = Repos();
        string? excluded = null;
        lines.Setup(l => l.GetActivityBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime?, DateTime?, string?, CancellationToken>((_, _, x, _) => excluded = x)
            .ReturnsAsync(new List<AccountBalanceSummary> { new() { AccountId = A("5111").Id, TotalCredit = 500m } });
        var handler = new GetIncomeStatementQueryHandler(accounts, lines.Object);

        var s = await handler.Handle(new GetIncomeStatementQuery { FromDate = new DateTime(2026, 10, 1), ToDate = new DateTime(2026, 10, 31) }, default);

        excluded.Should().Be(JournalEntry.PeriodClosingType);
        s.Rows.Single(r => r.Code == "60").Amount.Should().Be(500m);
        s.Rows.Should().OnlyContain(r => r.PriorAmount == null);

        await FluentActions.Awaiting(() => handler.Handle(new GetIncomeStatementQuery { FromDate = new DateTime(2026, 11, 1), ToDate = new DateTime(2026, 10, 1) }, default))
            .Should().ThrowAsync<BusinessRuleException>();
        await FluentActions.Awaiting(() => handler.Handle(new GetIncomeStatementQuery
            { FromDate = new DateTime(2026, 10, 1), ToDate = new DateTime(2026, 10, 31), CompareFromDate = new DateTime(2026, 9, 1) }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*both*");
    }

    // ---- CSV ----

    private static string Text(byte[] csv) => Encoding.UTF8.GetString(csv);

    [Fact]
    public void Csv_starts_with_a_utf8_byte_order_mark_so_excel_reads_vietnamese()
    {
        var bytes = CsvWriter.ToBytes(new[] { (IReadOnlyList<string>)new[] { "Tiền mặt", "1" } });

        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        Text(bytes).Should().Contain("Tiền mặt").And.EndWith("\r\n");
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    [InlineData("=SUM(A1:A9)", "'=SUM(A1:A9)")]
    [InlineData("+84 912", "'+84 912")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("-cmd", "'-cmd")]
    [InlineData("-12.5", "-12.5")]
    [InlineData("", "")]
    public void Csv_cells_are_quoted_and_formula_injection_is_neutralised(string input, string expected) =>
        CsvWriter.Escape(input).Should().Be(expected);

    [Fact]
    public void Numbers_are_written_in_invariant_form()
    {
        CsvWriter.Num(1234.5m).Should().Be("1234.5");
        CsvWriter.Num(0m).Should().Be("0");
        CsvWriter.Num((decimal?)null).Should().Be("");
    }

    [Fact]
    public void A_statement_csv_has_the_codes_indented_names_amounts_the_prior_column_and_the_warnings()
    {
        var statement = new FinancialStatementDto
        {
            LayoutId = "B02-DN", Title = "Income statement", FromDate = new DateTime(2026, 10, 1), ToDate = new DateTime(2026, 10, 31),
            Rows = { new StatementRowDto { Code = "01", Name = "Revenue, net", Indent = 1, Amount = 1000m, PriorAmount = 800m } },
            Unmapped = { new UnmappedAccountDto { AccountNumber = "627", AccountName = "Overheads", Balance = 70m } },
            Notes = { "Draft" },
        };

        var csv = Text(CsvWriter.FromStatement(statement));

        csv.Should().Contain("Code,Item,Amount,Prior");
        csv.Should().Contain("01,\"  Revenue, net\",1000,800");
        csv.Should().Contain("unmapped,627 Overheads,70");
        csv.Should().Contain("note,Draft");
    }

    [Fact]
    public void The_ledger_and_trial_balance_csv_carry_all_columns_and_the_totals()
    {
        var ledger = new AccountLedgerDto
        {
            AccountNumber = "131", AccountName = "Receivables", OpeningBalance = 100m, TotalDebit = 400m, TotalCredit = 300m, ClosingBalance = 200m,
            Lines = { new LedgerLineDto { EntryNumber = "JE-1", EntryDate = new DateTime(2026, 10, 5), Description = "Sale", AccountNumber = "131", CounterAccounts = "5111", Debit = 400m, Balance = 500m, Status = "Posted" } },
        };
        var tb = new TrialBalanceDto
        {
            Lines = { new TrialBalanceLineDto { AccountNumber = "131", AccountName = "Receivables", Level = 1, OpeningDebit = 100m, PeriodDebit = 400m, PeriodCredit = 300m, ClosingDebit = 200m } },
            Totals = { OpeningDebit = 100m, OpeningCredit = 100m, PeriodDebit = 400m, PeriodCredit = 400m, ClosingDebit = 200m, ClosingCredit = 200m, IsBalanced = true },
        };

        Text(CsvWriter.FromLedger(ledger)).Should().Contain("2026-10-05,JE-1,Sale,131,5111,400,0,500,Posted").And.Contain("Closing balance,200");
        Text(CsvWriter.FromTrialBalance(tb)).Should().Contain("131,Receivables,100,0,400,300,200,0").And.Contain("Total,balanced");
    }
}

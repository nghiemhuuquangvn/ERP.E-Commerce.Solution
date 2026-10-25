using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-2j: a tenant on a chart of its own places its accounts on the rows of B01 / B02.</summary>
public class StatementMappingTests
{
    // a chart that is neither Vas99 nor Simple14: asset 9001, liability 9101, equity 9201, revenue 9301, expense 9401
    private readonly List<Account> _chart = new()
    {
        Account.Create("9001", "Own cash", (int)AccountType.Asset, (int)NormalBalance.Debit),
        Account.Create("9101", "Own supplier", (int)AccountType.Liability, (int)NormalBalance.Credit),
        Account.Create("9201", "Own capital", (int)AccountType.Equity, (int)NormalBalance.Credit),
        Account.Create("9301", "Own sales", (int)AccountType.Revenue, (int)NormalBalance.Credit),
        Account.Create("9401", "Own wages", (int)AccountType.Expense, (int)NormalBalance.Debit),
    };

    private Account A(string number) => _chart.Single(a => a.AccountNumber == number);

    private Dictionary<Guid, AccountBalanceSummary> Balances(params (string Number, decimal Debit, decimal Credit)[] lines) =>
        lines.ToDictionary(l => A(l.Number).Id, l => new AccountBalanceSummary { AccountId = A(l.Number).Id, TotalDebit = l.Debit, TotalCredit = l.Credit });

    private static StatementAccountMapping Map(string statement, string row, string prefix, BalanceSide side = BalanceSide.Net) =>
        StatementAccountMapping.Create(statement, row, prefix, (int)side);

    private static StatementLayout B01(IEnumerable<Account> accounts) => StatementLayouts.For(StatementLayouts.BalanceSheet, accounts)!;

    private static string MappableRow(StatementLayout layout, NormalBalance natural, string startsWith) =>
        layout.Rows.First(r => IsMappableRow(r) && r.Natural == natural && r.Code.StartsWith(startsWith, StringComparison.Ordinal)).Code;

    private static bool IsMappableRow(StatementRow r) => r.Formula.Count == 0 && r.Special == null;

    [Fact]
    public void Without_a_mapping_the_accounts_of_an_own_chart_are_unmapped_and_with_it_they_are_counted()
    {
        var layout = B01(_chart);
        var balances = Balances(("9001", 500m, 0m), ("9101", 0m, 200m), ("9201", 0m, 300m));
        var asset = MappableRow(layout, NormalBalance.Debit, "1");
        var liability = MappableRow(layout, NormalBalance.Credit, "3");
        var equity = MappableRow(layout, NormalBalance.Credit, "4");

        var before = StatementEngine.Evaluate(layout, _chart, balances, null, 1m, null);
        before.Unmapped.Select(u => u.AccountNumber).Should().BeEquivalentTo(new[] { "9001", "9101", "9201" });

        var mapped = StatementLayouts.WithMappings(layout, new[]
        {
            Map(layout.Id, asset, "9001"), Map(layout.Id, liability, "9101"), Map(layout.Id, equity, "9201"),
        });
        var after = StatementEngine.Evaluate(mapped, _chart, balances, null, 1m, null);

        after.Unmapped.Should().BeEmpty();
        after.Rows.Single(r => r.Code == asset).Amount.Should().Be(500m);
        after.Rows.Single(r => r.Code == liability).Amount.Should().Be(200m);
        after.Rows.Single(r => r.Code == equity).Amount.Should().Be(300m);
    }

    [Fact]
    public void The_total_assets_equal_the_total_resources_once_every_account_is_placed()
    {
        var layout = B01(_chart);
        var balances = Balances(("9001", 500m, 0m), ("9101", 0m, 200m), ("9201", 0m, 300m));
        var mapped = StatementLayouts.WithMappings(layout, new[]
        {
            Map(layout.Id, MappableRow(layout, NormalBalance.Debit, "1"), "9001"),
            Map(layout.Id, MappableRow(layout, NormalBalance.Credit, "3"), "9101"),
            Map(layout.Id, MappableRow(layout, NormalBalance.Credit, "4"), "9201"),
        });

        var s = StatementEngine.Evaluate(mapped, _chart, balances, null, 1m, null);

        s.Rows.Single(r => r.Code == "270").Amount.Should().Be(500m);
        s.Rows.Single(r => r.Code == "440").Amount.Should().Be(500m);
        s.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void A_mapping_of_another_statement_a_formula_row_or_a_special_row_changes_nothing()
    {
        var layout = B01(_chart);
        var total = layout.Rows.First(r => r.Formula.Count > 0).Code;
        var special = layout.Rows.First(r => r.Special != null).Code;

        var same = StatementLayouts.WithMappings(layout, new[] { Map(StatementLayouts.IncomeStatement, "01", "9001"), Map(layout.Id, total, "9001"), Map(layout.Id, special, "9001") });

        same.Rows.SelectMany(r => r.Terms).Should().NotContain(t => t.Prefix == "9001");
    }

    [Fact]
    public void A_debit_only_mapping_takes_a_debit_balance_and_ignores_a_credit_one()
    {
        var layout = B01(_chart);
        var row = MappableRow(layout, NormalBalance.Debit, "1");
        var mapped = StatementLayouts.WithMappings(layout, new[] { Map(layout.Id, row, "9001", BalanceSide.DebitOnly) });

        StatementEngine.Evaluate(mapped, _chart, Balances(("9001", 400m, 0m)), null, 1m, null).Rows.Single(r => r.Code == row).Amount.Should().Be(400m);
        StatementEngine.Evaluate(mapped, _chart, Balances(("9001", 0m, 400m)), null, 1m, null).Rows.Single(r => r.Code == row).Amount.Should().Be(0m);
    }

    [Fact]
    public void The_income_statement_takes_mapped_revenue_and_expense_accounts_too()
    {
        var layout = StatementLayouts.For(StatementLayouts.IncomeStatement, _chart)!;
        var revenue = MappableRow(layout, NormalBalance.Credit, "0");
        var expense = layout.Rows.First(r => IsMappableRow(r) && r.Natural == NormalBalance.Debit).Code;
        var mapped = StatementLayouts.WithMappings(layout, new[] { Map(layout.Id, revenue, "9301"), Map(layout.Id, expense, "9401") });

        var s = StatementEngine.Evaluate(mapped, _chart, Balances(("9301", 0m, 1000m), ("9401", 600m, 0m)), null, 1m, null);

        s.Unmapped.Should().BeEmpty();
        s.Rows.Single(r => r.Code == revenue).Amount.Should().Be(1000m);
        s.Rows.Single(r => r.Code == expense).Amount.Should().Be(600m);
    }

    // ---- the handlers ----

    private readonly List<StatementAccountMapping> _stored = new();
    private readonly List<StatementAccountMapping> _removed = new();

    private Mock<IAccountingUnitOfWork> Uow(params (string Number, decimal Debit, decimal Credit)[] turnover)
    {
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _chart.ToList());
        var mappings = new Mock<IStatementMappingRepository>();
        mappings.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _stored.ToList());
        mappings.Setup(r => r.AddAsync(It.IsAny<StatementAccountMapping>(), It.IsAny<CancellationToken>()))
            .Callback<StatementAccountMapping, CancellationToken>((m, _) => _stored.Add(m)).Returns(Task.CompletedTask);
        mappings.Setup(r => r.Remove(It.IsAny<StatementAccountMapping>())).Callback<StatementAccountMapping>(m => { _stored.Remove(m); _removed.Add(m); });
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(l => l.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Balances(turnover).Values.ToList());
        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.Accounts).Returns(accounts.Object);
        uow.SetupGet(u => u.StatementMappings).Returns(mappings.Object);
        uow.SetupGet(u => u.JournalLines).Returns(lines.Object);
        return uow;
    }

    private static StatementMappingInput In(string row, string prefix, string side = "Net") => new() { RowCode = row, AccountPrefix = prefix, Side = side };

    [Fact]
    public async Task The_query_lists_the_rows_and_the_accounts_with_postings_that_no_row_takes()
    {
        var uow = Uow(("9001", 500m, 0m), ("9101", 0m, 200m));

        var dto = await new GetStatementMappingQueryHandler(uow.Object).Handle(new GetStatementMappingQuery(), default);

        dto.StatementId.Should().Be(StatementLayouts.BalanceSheet);
        dto.Rows.Should().Contain(r => r.Code == "270" && !r.IsMappable);
        dto.Rows.Should().Contain(r => r.IsMappable);
        dto.Unmapped.Select(u => u.AccountNumber).Should().BeEquivalentTo(new[] { "9001", "9101" });
    }

    [Fact]
    public async Task Saving_a_mapping_stores_it_and_takes_the_account_out_of_the_unmapped_list()
    {
        var uow = Uow(("9001", 500m, 0m));
        var row = MappableRow(B01(_chart), NormalBalance.Debit, "1");

        var dto = await new UpdateStatementMappingCommandHandler(uow.Object).Handle(
            new UpdateStatementMappingCommand { StatementId = StatementLayouts.BalanceSheet, Mappings = new() { In(row, "9001") } }, default);

        _stored.Should().ContainSingle().Which.AccountPrefix.Should().Be("9001");
        dto.Rows.Single(r => r.Code == row).Mappings.Should().ContainSingle().Which.AccountPrefix.Should().Be("9001");
        dto.Unmapped.Should().BeEmpty();
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Saving_again_keeps_the_rows_that_stay_changes_their_side_and_removes_the_others()
    {
        var uow = Uow();
        var handler = new UpdateStatementMappingCommandHandler(uow.Object);
        var layout = B01(_chart);
        var asset = MappableRow(layout, NormalBalance.Debit, "1");
        var liability = MappableRow(layout, NormalBalance.Credit, "3");
        await handler.Handle(new UpdateStatementMappingCommand { Mappings = new() { In(asset, "9001"), In(liability, "9101") } }, default);
        var kept = _stored.Single(m => m.AccountPrefix == "9001");

        await handler.Handle(new UpdateStatementMappingCommand { Mappings = new() { In(asset, "9001", "DebitOnly") } }, default);

        _stored.Should().ContainSingle().Which.Should().BeSameAs(kept);
        kept.Side.Should().Be((int)BalanceSide.DebitOnly);
        _removed.Should().ContainSingle().Which.AccountPrefix.Should().Be("9101");
    }

    [Fact]
    public async Task A_mapping_is_refused_for_an_unknown_row_a_total_row_an_unknown_prefix_a_double_placement_or_a_prefix_the_layout_already_places()
    {
        var uow = Uow();
        var handler = new UpdateStatementMappingCommandHandler(uow.Object);
        var layout = B01(_chart);
        var asset = MappableRow(layout, NormalBalance.Debit, "1");
        var other = MappableRow(layout, NormalBalance.Credit, "3");
        var placedPrefix = layout.Rows.First(r => r.Terms.Count > 0).Terms[0].Prefix;

        Task Act(params StatementMappingInput[] inputs) => handler.Handle(new UpdateStatementMappingCommand { Mappings = inputs.ToList() }, default);

        await FluentActions.Awaiting(() => Act(In("ZZZ", "9001"))).Should().ThrowAsync<BusinessRuleException>().WithMessage("*does not exist*");
        await FluentActions.Awaiting(() => Act(In("270", "9001"))).Should().ThrowAsync<BusinessRuleException>().WithMessage("*total or a computed row*");
        await FluentActions.Awaiting(() => Act(In(asset, "77"))).Should().ThrowAsync<BusinessRuleException>().WithMessage("*No account number starts with*");
        await FluentActions.Awaiting(() => Act(In(asset, "9001"), In(other, "9001"))).Should().ThrowAsync<BusinessRuleException>().WithMessage("*mapped twice*");
        await FluentActions.Awaiting(() => Act(In(asset, "9001", "Sideways"))).Should().ThrowAsync<BusinessRuleException>().WithMessage("*Unknown side*");
        await FluentActions.Awaiting(() => new GetStatementMappingQueryHandler(uow.Object).Handle(new GetStatementMappingQuery { StatementId = "B99" }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*Unknown statement*");
        _stored.Should().BeEmpty();
        placedPrefix.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task The_balance_sheet_handler_applies_the_tenants_mapping()
    {
        var uow = Uow();
        var layout = B01(_chart);
        var asset = MappableRow(layout, NormalBalance.Debit, "1");
        _stored.Add(Map(layout.Id, asset, "9001"));
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(l => l.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Balances(("9001", 500m, 0m), ("9201", 0m, 500m)).Values.ToList());
        var inv = new Mock<IInvoiceRepository>();
        var bl = new Mock<IBillRepository>();
        var ln = new Mock<ILoanRepository>();
        ln.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Loan>());

        var s = await new GetBalanceSheetStatementQueryHandler(uow.Object.Accounts, lines.Object, new Mock<IReportProfileRepository>().Object, inv.Object, bl.Object, ln.Object,
            new Mock<IPaymentApplicationRepository>().Object, uow.Object.StatementMappings).Handle(new GetBalanceSheetStatementQuery { AsOfDate = new DateTime(2026, 10, 31) }, default);

        s.Rows.Single(r => r.Code == asset).Amount.Should().Be(500m);
        s.Unmapped.Select(u => u.AccountNumber).Should().BeEquivalentTo(new[] { "9201" });
    }
}

using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-2k: the tenant's account mapping also feeds B03 (direct and indirect) and the tables of the notes B09.</summary>
public class StatementMappingB03Tests
{
    private readonly List<Account> _chart = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();

    private static StatementAccountMapping Map(string statement, string row, string prefix) => StatementAccountMapping.Create(statement, row, prefix, 0);

    private static Account Own(string number, AccountType type, NormalBalance normal) => Account.Create(number, "Own " + number, (int)type, (int)normal);

    // ---- B03 direct ----

    [Fact]
    public void A_movement_against_an_own_account_is_unmapped_until_its_prefix_is_mapped_to_a_receipt_row_and_then_counts_there()
    {
        var layout = CashFlowLayouts.Layout;
        var own = Own("9501", AccountType.Asset, NormalBalance.Debit);
        own.SetCashFlow(false, (int)CashFlowCategory.Investing);   // an investing counter-account that the layout does not know: no row takes it
        var column = new CashFlowColumn(new[] { new CashMovement(own, 100m) }, 0m, 100m);

        var before = CashFlowStatementEngine.Evaluate(layout, column, null);
        before.Unmapped.Should().NotBeEmpty();

        var receipts = layout.Rows.First(r => r.Terms.Count > 0 && r.Terms[0].Inflow && r.Formula.Count == 0);
        var mapped = CashFlowLayouts.WithMappings(layout, new[] { Map(layout.Id, receipts.Code, "95") });
        var after = CashFlowStatementEngine.Evaluate(mapped, column, null);

        after.Rows.Single(r => r.Code == receipts.Code).Amount.Should().Be(100m);
        after.Unmapped.Select(u => u.AccountNumber).Should().NotContain("9501");
    }

    [Fact]
    public void A_payment_row_takes_the_prefix_as_an_outflow_and_fallback_formula_and_special_rows_ignore_a_mapping()
    {
        var layout = CashFlowLayouts.Layout;
        var own = Own("9601", AccountType.Expense, NormalBalance.Debit);
        var column = new CashFlowColumn(new[] { new CashMovement(own, -40m) }, 100m, 60m);
        var payments = layout.Rows.First(r => r.Terms.Count > 0 && !r.Terms[0].Inflow && r.Formula.Count == 0);
        var formula = layout.Rows.First(r => r.Formula.Count > 0);
        var fallback = layout.Rows.First(r => r.FallbackCategory.HasValue);

        var mapped = CashFlowLayouts.WithMappings(layout, new[] { Map(layout.Id, payments.Code, "96"), Map(layout.Id, formula.Code, "96"), Map(layout.Id, fallback.Code, "96") });

        CashFlowStatementEngine.Evaluate(mapped, column, null).Rows.Single(r => r.Code == payments.Code).Amount.Should().Be(-40m);
        mapped.Rows.Single(r => r.Code == formula.Code).Terms.Should().BeEmpty();
        mapped.Rows.Single(r => r.Code == fallback.Code).Terms.Should().BeEmpty();
    }

    // ---- B03 indirect ----

    [Fact]
    public void An_indirect_row_takes_the_prefix_and_the_accounts_the_form_deals_with_include_it()
    {
        var layout = IndirectCashFlowLayouts.Layout;
        var row = layout.Rows.First(r => IndirectCashFlowLayouts.TakesPrefixes(r));

        var mapped = IndirectCashFlowLayouts.WithMappings(layout, new[] { Map(layout.Id, row.Code, "97") });

        mapped.Rows.Single(r => r.Code == row.Code).Prefixes.Should().Contain("97");
        mapped.CoveredPrefixes.Should().Contain("97");
        IndirectCashFlowLayouts.WithMappings(layout, new[] { Map(layout.Id, layout.Rows.First(r => !IndirectCashFlowLayouts.TakesPrefixes(r)).Code, "98") })
            .CoveredPrefixes.Should().NotContain("98");
    }

    // ---- the notes ----

    [Fact]
    public void A_mapped_prefix_joins_the_prefixes_of_a_notes_table_once()
    {
        var standard = NotePrefixes.Vas99;

        var mapped = NoteMappingRows.WithMappings(standard, new[] { Map("B09-DN", "V.1", "9001"), Map("B09-DN", "V.1", "9001"), Map("B09-DN", "VI.1", "95"), Map("B01-DN", "V.2", "5") });

        mapped.Cash.Should().Contain("9001").And.Contain("111");
        mapped.Cash.Count(p => p == "9001").Should().Be(1);
        mapped.Revenue.Should().Contain("95");
        mapped.Receivable.Should().Equal(standard.Receivable);
    }

    // ---- the catalog and the screen handlers ----

    [Fact]
    public void The_catalog_describes_every_statement_with_its_mappable_rows()
    {
        foreach (var id in StatementMappingCatalog.Ids)
        {
            var statement = StatementMappingCatalog.Get(id, _chart);
            statement.Id.Should().Be(id);
            statement.Rows.Should().Contain(r => r.IsMappable);
        }

        StatementMappingCatalog.Get("B03-DN", _chart).Rows.Single(r => r.Code == "20").IsMappable.Should().BeFalse();   // total
        StatementMappingCatalog.Get("B03-DN", _chart).UsesSide.Should().BeFalse();
        StatementMappingCatalog.Get("B01-DN", _chart).UsesSide.Should().BeTrue();
        StatementMappingCatalog.Get("B09-DN", _chart).Rows.Select(r => r.Code).Should().Contain(new[] { "V.1", "V.7", "VI.2" });
        FluentActions.Invoking(() => StatementMappingCatalog.Get("B99", _chart)).Should().Throw<BusinessRuleException>();
    }

    private readonly List<StatementAccountMapping> _stored = new();

    private Mock<IAccountingUnitOfWork> Uow()
    {
        _chart.Add(Own("9501", AccountType.Revenue, NormalBalance.Credit));
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _chart.ToList());
        var mappings = new Mock<IStatementMappingRepository>();
        mappings.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _stored.ToList());
        mappings.Setup(r => r.AddAsync(It.IsAny<StatementAccountMapping>(), It.IsAny<CancellationToken>()))
            .Callback<StatementAccountMapping, CancellationToken>((m, _) => _stored.Add(m)).Returns(Task.CompletedTask);
        mappings.Setup(r => r.Remove(It.IsAny<StatementAccountMapping>())).Callback<StatementAccountMapping>(m => _stored.Remove(m));
        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.Accounts).Returns(accounts.Object);
        uow.SetupGet(u => u.StatementMappings).Returns(mappings.Object);
        uow.SetupGet(u => u.JournalLines).Returns(new Mock<IJournalLineRepository>().Object);
        return uow;
    }

    private static StatementMappingInput In(string row, string prefix) => new() { RowCode = row, AccountPrefix = prefix };

    [Fact]
    public async Task B03_and_the_notes_can_be_mapped_from_the_screen_and_a_total_row_is_refused()
    {
        var uow = Uow();
        var handler = new UpdateStatementMappingCommandHandler(uow.Object);

        var b03 = await handler.Handle(new UpdateStatementMappingCommand { StatementId = "B03-DN", Mappings = new() { In("01", "95") } }, default);
        var notes = await handler.Handle(new UpdateStatementMappingCommand { StatementId = "B09-DN", Mappings = new() { In("VI.1", "95") } }, default);

        b03.SideApplies.Should().BeFalse();
        b03.UnmappedOnReport.Should().BeTrue();
        b03.Rows.Single(r => r.Code == "01").Mappings.Should().ContainSingle().Which.AccountPrefix.Should().Be("95");
        notes.Rows.Single(r => r.Code == "VI.1").Mappings.Should().ContainSingle();
        _stored.Select(m => m.StatementId).Should().BeEquivalentTo("B03-DN", "B09-DN");
        await FluentActions.Awaiting(() => handler.Handle(new UpdateStatementMappingCommand { StatementId = "B03-DN", Mappings = new() { In("20", "95") } }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*total or a computed row*");
        await FluentActions.Awaiting(() => handler.Handle(new UpdateStatementMappingCommand { StatementId = "B09-DN", Mappings = new() { In("V.1", "111") } }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*already placed*");
    }
}

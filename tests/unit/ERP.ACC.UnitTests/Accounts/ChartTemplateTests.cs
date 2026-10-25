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

namespace ERP.ACC.UnitTests.Accounts;

/// <summary>ROADMAP P1-2: the shipped charts are internally consistent and can be applied to a tenant that has no history.</summary>
public class ChartTemplateTests
{
    public static IEnumerable<object[]> Templates() => ChartTemplates.All.Select(t => new object[] { t.Id });

    [Theory, MemberData(nameof(Templates))]
    public void Every_template_has_unique_numbers_and_parents_listed_before_their_children_with_the_same_type(string id)
    {
        var template = ChartTemplates.Find(id)!;

        template.Accounts.Select(a => a.Number).Should().OnlyHaveUniqueItems();
        var seen = new Dictionary<string, ChartTemplate.Entry>();
        foreach (var entry in template.Accounts)
        {
            if (entry.ParentNumber != null)
            {
                seen.Should().ContainKey(entry.ParentNumber, $"{entry.Number} needs its parent {entry.ParentNumber} listed first");
                seen[entry.ParentNumber].Type.Should().Be(entry.Type, $"{entry.Number} has the type of its parent");
            }
            seen[entry.Number] = entry;
        }
    }

    [Theory, MemberData(nameof(Templates))]
    public void Every_posting_key_maps_to_an_existing_leaf_account_and_receivables_differ_from_payables(string id)
    {
        var template = ChartTemplates.Find(id)!;
        var parents = template.Accounts.Where(a => a.ParentNumber != null).Select(a => a.ParentNumber!).ToHashSet();

        foreach (var key in PostingKeys.All)
        {
            template.PostingMap.Should().ContainKey(key, $"{template.Id} must map {key}");
            var number = template.PostingMap[key];
            template.Accounts.Should().Contain(a => a.Number == number, $"{key} -> {number} must exist in {template.Id}");
            parents.Should().NotContain(number, $"{key} -> {number} must be a leaf (parents take no postings)");
        }
        template.PostingMap[PostingKeys.AccountsReceivable].Should().NotBe(template.PostingMap[PostingKeys.AccountsPayable]);
    }

    [Fact]
    public void The_vietnamese_template_covers_the_classes_and_keeps_contra_accounts_on_the_opposite_side()
    {
        var vas = ChartTemplates.Find(ChartTemplates.Vas99)!;

        vas.Accounts.Select(a => a.Number[0]).Distinct().Order().Should().Equal('0', '1', '2', '3', '4', '5', '6', '7', '8', '9'); // 0 = off-balance accounts
        vas.Accounts.Single(a => a.Number == "214").Normal.Should().Be(NormalBalance.Credit); // accumulated depreciation, an asset
        vas.Accounts.Single(a => a.Number == "214").Type.Should().Be(AccountType.Asset);
        vas.Accounts.Single(a => a.Number == "521").Normal.Should().Be(NormalBalance.Debit);  // sales deductions, revenue class
        vas.Accounts.Single(a => a.Number == "911").Type.Should().Be(AccountType.Equity);
        vas.PostingMap[PostingKeys.IncomeSummary].Should().Be("911");
        vas.PostingMap[PostingKeys.RetainedEarnings].Should().Be("4212");
    }

    [Fact]
    public void Building_a_template_sets_parent_links_and_levels()
    {
        var accounts = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();

        var cash = accounts.Single(a => a.AccountNumber == "111");
        var vnd = accounts.Single(a => a.AccountNumber == "1111");
        cash.Level.Should().Be(1);
        cash.ParentAccountId.Should().BeNull();
        vnd.ParentAccountId.Should().Be(cash.Id);
        vnd.Level.Should().Be(2);
    }

    [Theory, MemberData(nameof(Templates))]
    public void Every_template_has_cash_accounts_and_the_cash_flow_defaults_are_sensible(string id)
    {
        var template = ChartTemplates.Find(id)!;
        var accounts = template.BuildAccounts();

        accounts.Where(a => a.IsCash).Should().NotBeEmpty($"{template.Id} needs cash accounts for the cash-flow statement");
        accounts.Where(a => a.IsCash).Should().OnlyContain(a => a.AccountType == (int)AccountType.Asset && a.NormalBalance == (int)NormalBalance.Debit);
        // the posting keys Cash and Bank point at flagged cash accounts
        foreach (var key in new[] { PostingKeys.Cash, PostingKeys.Bank })
            accounts.Single(a => a.AccountNumber == template.PostingMap[key]).IsCash.Should().BeTrue($"{key} must be a cash account");
        // equity is financing unless overridden; revenue / expense accounts are operating by default
        accounts.Where(a => a.AccountType == (int)AccountType.Equity && a.CashFlowCategory == null)
            .Should().OnlyContain(a => a.EffectiveCashFlowCategory == CashFlowCategory.Financing);
        accounts.Where(a => a.AccountType is (int)AccountType.Revenue or (int)AccountType.Expense)
            .Should().OnlyContain(a => a.EffectiveCashFlowCategory == CashFlowCategory.Operating);
    }

    [Fact]
    public void The_vietnamese_template_classifies_fixed_assets_as_investing_and_borrowings_as_financing()
    {
        var accounts = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();

        accounts.Single(a => a.AccountNumber == "211").EffectiveCashFlowCategory.Should().Be(CashFlowCategory.Investing);
        accounts.Single(a => a.AccountNumber == "341").EffectiveCashFlowCategory.Should().Be(CashFlowCategory.Financing);
        accounts.Single(a => a.AccountNumber == "4111").EffectiveCashFlowCategory.Should().Be(CashFlowCategory.Financing);
        accounts.Single(a => a.AccountNumber == "131").EffectiveCashFlowCategory.Should().Be(CashFlowCategory.Operating);
        accounts.Single(a => a.AccountNumber == "1121").IsCash.Should().BeTrue();
    }

    [Fact]
    public void An_unknown_cash_flow_category_is_refused_and_null_goes_back_to_the_default()
    {
        var account = Account.Create("131", "Receivables", (int)AccountType.Asset, (int)NormalBalance.Debit);

        account.Invoking(a => a.SetCashFlow(false, 9)).Should().Throw<ArgumentOutOfRangeException>();
        account.SetCashFlow(false, 2);
        account.EffectiveCashFlowCategory.Should().Be(CashFlowCategory.Investing);
        account.SetCashFlow(false, null);
        account.EffectiveCashFlowCategory.Should().Be(CashFlowCategory.Operating);
    }

    [Fact]
    public void Template_lookup_ignores_case_and_returns_null_for_unknown_ids()
    {
        ChartTemplates.Find("vas99").Should().NotBeNull();
        ChartTemplates.Find("nope").Should().BeNull();
    }

    // ---- apply ----

    private readonly List<Account> _accounts = new();
    private readonly List<PostingAccountMapping> _mappings = new();
    private readonly Mock<IJournalEntryRepository> _entries = new();
    private readonly Mock<IAccountingUnitOfWork> _uow = new();
    private static readonly Guid Tenant = Guid.NewGuid();

    private ApplyChartTemplateCommandHandler Handler()
    {
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllTrackedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _accounts.ToList());
        accounts.Setup(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Callback<Account, CancellationToken>((a, _) => _accounts.Add(a)).Returns(Task.CompletedTask);
        var mappings = new Mock<IPostingAccountMappingRepository>();
        mappings.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _mappings.ToList());
        mappings.Setup(r => r.AddAsync(It.IsAny<PostingAccountMapping>(), It.IsAny<CancellationToken>()))
            .Callback<PostingAccountMapping, CancellationToken>((m, _) => _mappings.Add(m)).Returns(Task.CompletedTask);
        _uow.SetupGet(u => u.Accounts).Returns(accounts.Object);
        _uow.SetupGet(u => u.PostingMappings).Returns(mappings.Object);
        _uow.SetupGet(u => u.JournalEntries).Returns(_entries.Object);
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(t => t.CurrentTenantId).Returns(Tenant);
        return new ApplyChartTemplateCommandHandler(_uow.Object, tenant.Object);
    }

    [Fact]
    public async Task Applying_the_vietnamese_template_creates_the_accounts_and_points_every_posting_key_at_it()
    {
        var result = await Handler().Handle(new ApplyChartTemplateCommand { TemplateId = "Vas99" }, default);

        result.AccountsCreated.Should().Be(ChartTemplates.Find("Vas99")!.Accounts.Count);
        result.PostingKeysMapped.Should().Be(PostingKeys.All.Count);
        var receivable = _accounts.Single(a => a.AccountNumber == "131");
        _mappings.Single(m => m.PostingKey == PostingKeys.AccountsReceivable).AccountId.Should().Be(receivable.Id);
        _accounts.Single(a => a.AccountNumber == "1111").ParentAccountId.Should().Be(_accounts.Single(a => a.AccountNumber == "111").Id);
    }

    [Fact]
    public async Task Applying_again_adds_nothing_and_re_points_the_profile()
    {
        var handler = Handler();
        await handler.Handle(new ApplyChartTemplateCommand { TemplateId = "Vas99" }, default);
        var count = _accounts.Count;

        var second = await handler.Handle(new ApplyChartTemplateCommand { TemplateId = "vas99" }, default);

        second.AccountsCreated.Should().Be(0);
        _accounts.Count.Should().Be(count);
        _mappings.Select(m => m.PostingKey).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task A_tenant_with_journal_entries_or_an_unknown_template_is_refused()
    {
        var handler = Handler();
        _entries.Setup(e => e.AnyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await FluentActions.Awaiting(() => handler.Handle(new ApplyChartTemplateCommand { TemplateId = "Vas99" }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*journal entries*");
        await FluentActions.Awaiting(() => handler.Handle(new ApplyChartTemplateCommand { TemplateId = "Mars" }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*Unknown chart template*");
        _accounts.Should().BeEmpty();
    }
}

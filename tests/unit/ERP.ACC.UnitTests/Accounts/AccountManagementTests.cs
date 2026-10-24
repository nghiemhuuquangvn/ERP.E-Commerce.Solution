using ERP.ACC.Application.Commands;
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

namespace ERP.ACC.UnitTests.Accounts;

/// <summary>ROADMAP P1-2: a chart of accounts with parents, sub-accounts and a safe way to retire an account.</summary>
public class AccountManagementTests
{
    private readonly List<Account> _accounts = new();
    private readonly List<PostingAccountMapping> _mappings = new();
    private readonly List<AccountBalanceSummary> _balances = new();
    private readonly Mock<IAccountingUnitOfWork> _uow = new();

    public AccountManagementTests()
    {
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _accounts.ToList());
        accounts.Setup(r => r.GetAllTrackedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _accounts.ToList());
        accounts.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _accounts.FirstOrDefault(a => a.Id == id));
        accounts.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => _accounts.Where(x => ids.Contains(x.Id)).ToList());
        accounts.Setup(r => r.GetByNumbersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> numbers, CancellationToken _) => _accounts.Where(a => numbers.Contains(a.AccountNumber)).ToList());
        accounts.Setup(r => r.GetParentAccountIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _accounts.Where(a => a.IsActive && a.ParentAccountId.HasValue).Select(a => a.ParentAccountId!.Value).ToHashSet());
        accounts.Setup(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Callback<Account, CancellationToken>((a, _) => _accounts.Add(a)).Returns(Task.CompletedTask);

        var mappings = new Mock<IPostingAccountMappingRepository>();
        mappings.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _mappings.ToList());

        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(r => r.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _balances.ToList());

        _uow.SetupGet(u => u.Accounts).Returns(accounts.Object);
        _uow.SetupGet(u => u.PostingMappings).Returns(mappings.Object);
        _uow.SetupGet(u => u.JournalLines).Returns(lines.Object);
    }

    private Account Add(string number, AccountType type = AccountType.Asset, Account? parent = null)
    {
        var account = Account.Create(number, "Account " + number, (int)type, type is AccountType.Liability or AccountType.Equity or AccountType.Revenue ? 2 : 1, parent);
        _accounts.Add(account);
        return account;
    }

    private Task<ERP.ACC.Application.DTOs.AccountDto> Create(string number, Guid? parent = null, AccountType type = AccountType.Asset) =>
        new CreateAccountCommandHandler(_uow.Object).Handle(new CreateAccountCommand
        {
            AccountNumber = number, Name = "New " + number, AccountType = (int)type, NormalBalance = 1, ParentAccountId = parent
        }, default);

    [Fact]
    public async Task A_sub_account_sits_one_level_below_its_parent_and_has_the_same_type()
    {
        var cash = Add("111");

        var dto = await Create("1111", cash.Id);

        dto.ParentAccountId.Should().Be(cash.Id);
        dto.Level.Should().Be(2);
        dto.AccountClass.Should().Be(1);
    }

    [Fact]
    public async Task A_duplicate_number_a_missing_parent_and_a_different_type_are_business_rule_errors()
    {
        var cash = Add("111");

        (await FluentActions.Awaiting(() => Create("111")).Should().ThrowAsync<BusinessRuleException>()).WithMessage("*already exists*");
        await FluentActions.Awaiting(() => Create("1112", Guid.NewGuid())).Should().ThrowAsync<BusinessRuleException>();
        await FluentActions.Awaiting(() => Create("1113", cash.Id, AccountType.Liability)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Moving_an_account_recalculates_the_levels_of_its_sub_accounts_and_refuses_cycles()
    {
        var a = Add("11");
        var b = Add("111", parent: a);
        var c = Add("1111", parent: b);
        var d = Add("12");
        var handler = new UpdateAccountCommandHandler(_uow.Object);

        await handler.Handle(new UpdateAccountCommand { AccountId = b.Id, Name = "Cash", ParentAccountId = d.Id }, default);

        b.ParentAccountId.Should().Be(d.Id);
        c.Level.Should().Be(3);

        await FluentActions.Awaiting(() => handler.Handle(new UpdateAccountCommand { AccountId = d.Id, Name = "x", ParentAccountId = c.Id }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*under itself*");
    }

    [Fact]
    public async Task An_account_is_deactivated_only_when_nothing_depends_on_it()
    {
        var parent = Add("111");
        var child = Add("1111", parent: parent);
        var mapped = Add("112");
        var withBalance = Add("113");
        var free = Add("114");
        _mappings.Add(PostingAccountMapping.Create(Guid.NewGuid(), PostingKeys.Bank, mapped.Id));
        _balances.Add(new AccountBalanceSummary { AccountId = withBalance.Id, TotalDebit = 500m, TotalCredit = 0m });
        var handler = new SetAccountActiveCommandHandler(_uow.Object);

        await FluentActions.Awaiting(() => handler.Handle(new SetAccountActiveCommand { AccountId = parent.Id }, default)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*sub-accounts*");
        await FluentActions.Awaiting(() => handler.Handle(new SetAccountActiveCommand { AccountId = mapped.Id }, default)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*posting profile*");
        await FluentActions.Awaiting(() => handler.Handle(new SetAccountActiveCommand { AccountId = withBalance.Id }, default)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*balance*");

        var dto = await handler.Handle(new SetAccountActiveCommand { AccountId = free.Id }, default);
        dto.IsActive.Should().BeFalse();
        free.IsActive.Should().BeFalse();

        var reactivated = await handler.Handle(new SetAccountActiveCommand { AccountId = free.Id, Active = true }, default);
        reactivated.IsActive.Should().BeTrue();

        child.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task The_tree_nests_sub_accounts_and_marks_only_leaves_as_postable()
    {
        var cash = Add("111");
        Add("1111", parent: cash);
        Add("1112", parent: cash);
        Add("131");

        var tree = await new GetAccountTreeQueryHandler(_uow.Object.Accounts).Handle(new GetAccountTreeQuery(), default);

        tree.Select(n => n.Account.AccountNumber).Should().Equal("111", "131");
        var node = tree.Single(n => n.Account.AccountNumber == "111");
        node.Account.IsPostable.Should().BeFalse();
        node.Children.Select(c => c.Account.AccountNumber).Should().Equal("1111", "1112");
        node.Children.Should().OnlyContain(c => c.Account.IsPostable);
        tree.Single(n => n.Account.AccountNumber == "131").Account.IsPostable.Should().BeTrue();
    }

    [Fact]
    public async Task A_parent_account_takes_no_manual_postings_and_cannot_back_a_posting_key()
    {
        var cash = Add("111");
        var sub = Add("1111", parent: cash);
        var revenue = Add("511", AccountType.Revenue);
        var parents = await AccountRules_ParentIds();

        parents.Should().Contain(cash.Id);
        var lines = new Mock<IJournalEntryRepository>();
        var periods = new Mock<IFiscalPeriodRepository>();
        var period = FiscalPeriod.Create("October 2026", new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));
        periods.Setup(p => p.GetByIdAsync(period.Id, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _uow.SetupGet(u => u.JournalEntries).Returns(lines.Object);
        _uow.SetupGet(u => u.FiscalPeriods).Returns(periods.Object);

        var handler = new PostJournalEntryCommandHandler(_uow.Object);
        var command = new PostJournalEntryCommand
        {
            EntryNumber = "JE-X", FiscalPeriodId = period.Id, Description = "x", EntryDate = new DateTime(2026, 10, 5),
            Lines = new List<JournalLineDto>
            {
                new() { AccountId = cash.Id, Debit = 10m },
                new() { AccountId = revenue.Id, Credit = 10m },
            }
        };

        await FluentActions.Awaiting(() => handler.Handle(command, default)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*sub-accounts*");

        command.Lines[0].AccountId = sub.Id;
        await handler.Handle(command, default);
        lines.Verify(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private Task<HashSet<Guid>> AccountRules_ParentIds() => _uow.Object.Accounts.GetParentAccountIdsAsync();
}

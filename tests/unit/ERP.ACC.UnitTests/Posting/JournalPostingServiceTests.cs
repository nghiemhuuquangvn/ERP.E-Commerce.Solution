using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-1: the engine every automatic posting goes through.</summary>
public class JournalPostingServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    // The "database" the mocked repositories read from, and what the service staged on the unit of work.
    private readonly List<Account> _accounts = new();
    private readonly List<PostingAccountMapping> _mappings = new();
    private readonly List<FiscalPeriod> _periods = new();
    private readonly List<JournalEntry> _entries = new();
    private readonly List<JournalEntry> _added = new();
    private readonly List<FiscalPeriod> _addedPeriods = new();
    private readonly List<Account> _addedAccounts = new();
    private readonly List<PostingAccountMapping> _addedMappings = new();
    private int _sequence;

    private JournalPostingService Service(bool enabled = true, Guid? tenant = null)
    {
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _accounts.ToList());
        accounts.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => _accounts.Where(a => ids.Contains(a.Id)).ToList());
        accounts.Setup(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Callback<Account, CancellationToken>((a, _) => _addedAccounts.Add(a)).Returns(Task.CompletedTask);

        var mappings = new Mock<IPostingAccountMappingRepository>();
        mappings.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _mappings.ToList());
        mappings.Setup(r => r.AddAsync(It.IsAny<PostingAccountMapping>(), It.IsAny<CancellationToken>()))
            .Callback<PostingAccountMapping, CancellationToken>((m, _) => _addedMappings.Add(m)).Returns(Task.CompletedTask);

        var periods = new Mock<IFiscalPeriodRepository>();
        periods.Setup(r => r.GetContainingAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime d, CancellationToken _) => _periods.FirstOrDefault(p => p.StartDate <= d.Date && d.Date <= p.EndDate));
        periods.Setup(r => r.AddAsync(It.IsAny<FiscalPeriod>(), It.IsAny<CancellationToken>()))
            .Callback<FiscalPeriod, CancellationToken>((p, _) => _addedPeriods.Add(p)).Returns(Task.CompletedTask);

        var entries = new Mock<IJournalEntryRepository>();
        entries.Setup(r => r.GetBySourceKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => _entries.FirstOrDefault(e => e.SourceKey == key));
        entries.Setup(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
            .Callback<JournalEntry, CancellationToken>((e, _) => _added.Add(e)).Returns(Task.CompletedTask);
        entries.Setup(r => r.UpdateAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.Accounts).Returns(accounts.Object);
        uow.SetupGet(u => u.PostingMappings).Returns(mappings.Object);
        uow.SetupGet(u => u.FiscalPeriods).Returns(periods.Object);
        uow.SetupGet(u => u.JournalEntries).Returns(entries.Object);

        var numbers = new Mock<IJournalNumberGenerator>();
        numbers.Setup(n => n.NextAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, int year, CancellationToken _) => $"JE-{year}-{++_sequence:D6}");

        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.SetupGet(t => t.CurrentTenantId).Returns(tenant ?? Tenant);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Accounting:AutoPosting:Enabled"] = enabled.ToString() })
            .Build();

        return new JournalPostingService(uow.Object, numbers.Object, tenantProvider.Object, configuration,
            NullLogger<JournalPostingService>.Instance);
    }

    private static PostingRequest Invoice(Guid sourceId, DateTime date, decimal amount = 500m) =>
        new("Invoice", sourceId, date, "Invoice INV-1",
            new[] { PostingLine.Dr(PostingKeys.AccountsReceivable, amount), PostingLine.Cr(PostingKeys.SalesRevenue, amount) });

    private static Account NewAccount(string number, bool active = true)
    {
        var account = Account.Create(number, "Account " + number, (int)AccountType.Asset, (int)NormalBalance.Debit);
        account.IsActive = active;
        return account;
    }

    private void OwnChartWithMapping(bool includeReceivable = true, bool receivableActive = true)
    {
        foreach (var key in PostingKeys.All)
        {
            if (key == PostingKeys.AccountsReceivable && !includeReceivable)
                continue;
            var account = NewAccount("X-" + key, key != PostingKeys.AccountsReceivable || receivableActive);
            _accounts.Add(account);
            _mappings.Add(PostingAccountMapping.Create(Tenant, key, account.Id));
        }
    }

    private void OpenPeriod(DateTime month, bool closed = false)
    {
        var period = FiscalPeriod.Create(month.ToString("MMMM yyyy"), new DateTime(month.Year, month.Month, 1),
            new DateTime(month.Year, month.Month, 1).AddMonths(1).AddDays(-1));
        if (closed)
            period.Close();
        _periods.Add(period);
    }

    [Fact]
    public async Task ANewTenant_GetsTheDefaultChart_AMappingForEveryKey_AnOpenMonth_AndTheEntry()
    {
        var sourceId = Guid.NewGuid();

        var entry = await Service().PostAsync(Invoice(sourceId, new DateTime(2026, 10, 23)));

        entry!.Number().Should().Be("JE-2026-000001");
        entry.Status.Should().Be((int)JournalStatus.Posted);
        entry.EntryDate.Should().Be(new DateTime(2026, 10, 23));
        entry.SourceKey.Should().Be($"Invoice:{sourceId:N}");
        _addedAccounts.Should().HaveCount(DefaultChartOfAccounts.Accounts.Count);
        _addedMappings.Select(m => m.PostingKey).Should().BeEquivalentTo(PostingKeys.All);
        var receivable = _addedAccounts.Single(a => a.AccountNumber == "1100");
        var revenue = _addedAccounts.Single(a => a.AccountNumber == "4000");
        entry.Lines.Single(l => l.Debit > 0).AccountId.Should().Be(receivable.Id);
        entry.Lines.Single(l => l.Credit > 0).AccountId.Should().Be(revenue.Id);
        _addedPeriods.Should().ContainSingle().Which.PeriodName.Should().Be("October 2026");
        entry.FiscalPeriodId.Should().Be(_addedPeriods.Single().Id);
        _added.Should().ContainSingle();
    }

    [Fact]
    public async Task AMappingFromBeforeTheStockKeys_IsToppedUpFromTheDefaultAccounts_WithoutTouchingTheExistingKeys()
    {
        // The SYSTEM tenant after P1-1 phase 1: default chart, mapping rows for the first seven keys only.
        foreach (var entry in DefaultChartOfAccounts.Accounts)
            _accounts.Add(DefaultChartOfAccounts.ToAccount(entry));
        var oldKeys = new[] { PostingKeys.Cash, PostingKeys.Bank, PostingKeys.AccountsReceivable, PostingKeys.AccountsPayable,
            PostingKeys.SalesRevenue, PostingKeys.Inventory, PostingKeys.VatOutput };
        foreach (var key in oldKeys)
            _mappings.Add(PostingAccountMapping.Create(Tenant, key,
                _accounts.Single(a => a.AccountNumber == PostingKeys.DefaultAccountNumbers[key]).Id));

        var stock = PostingRules.ForStockMovement(Guid.NewGuid(), "Sold", -120m, new DateTime(2026, 10, 23), null)!;
        var posted = await Service().PostAsync(stock);

        _addedMappings.Select(m => m.PostingKey).Should().BeEquivalentTo(
            new[] { PostingKeys.GoodsReceivedNotInvoiced, PostingKeys.CostOfGoodsSold, PostingKeys.InventoryAdjustment });
        posted!.Lines.Single(l => l.Debit > 0).AccountId.Should().Be(_accounts.Single(a => a.AccountNumber == "5000").Id);
        posted.Lines.Single(l => l.Credit > 0).AccountId.Should().Be(_accounts.Single(a => a.AccountNumber == "1200").Id);
    }

    [Fact]
    public async Task ATenantWhoseChartLacksTheStockAccounts_StillPostsInvoices_AndOnlyStockPostingFails()
    {
        _accounts.Add(NewAccount("111"));
        _accounts.Add(NewAccount("131"));
        _accounts.Add(NewAccount("511"));
        _mappings.Add(PostingAccountMapping.Create(Tenant, PostingKeys.AccountsReceivable, _accounts[1].Id));
        _mappings.Add(PostingAccountMapping.Create(Tenant, PostingKeys.SalesRevenue, _accounts[2].Id));
        OpenPeriod(new DateTime(2026, 10, 1));

        var invoice = await Service().PostAsync(Invoice(Guid.NewGuid(), new DateTime(2026, 10, 23)));
        invoice.Should().NotBeNull();

        var stock = PostingRules.ForStockMovement(Guid.NewGuid(), "Received", 500m, new DateTime(2026, 10, 23), null)!;
        var act = () => Service().PostAsync(stock);
        await act.Should().ThrowAsync<PostingConfigurationException>();
    }

    [Fact]
    public async Task ATenantWithItsOwnMapping_UsesIt_AndGetsNoExtraAccounts()
    {
        OwnChartWithMapping();
        OpenPeriod(new DateTime(2026, 10, 1));

        var entry = await Service().PostAsync(Invoice(Guid.NewGuid(), new DateTime(2026, 10, 5)));

        var receivable = _accounts.Single(a => a.AccountNumber == "X-AccountsReceivable");
        entry!.Lines.Single(l => l.Debit > 0).AccountId.Should().Be(receivable.Id);
        _addedAccounts.Should().BeEmpty();
        _addedMappings.Should().BeEmpty();
        _addedPeriods.Should().BeEmpty("the existing October period contains the date");
        entry.FiscalPeriodId.Should().Be(_periods.Single().Id);
    }

    [Fact]
    public async Task APostedSource_IsNotPostedAgain()
    {
        OwnChartWithMapping();
        OpenPeriod(new DateTime(2026, 10, 1));
        var sourceId = Guid.NewGuid();
        var first = await Service().PostAsync(Invoice(sourceId, new DateTime(2026, 10, 5)));
        _entries.Add(first!); // "saved"
        _added.Clear();

        var again = await Service().PostAsync(Invoice(sourceId, new DateTime(2026, 10, 5)));

        again.Should().BeSameAs(first);
        _added.Should().BeEmpty("a redelivered event finds the entry that already exists");
    }

    [Fact]
    public async Task TheSameSourceTwiceInOneUnitOfWork_StagesOneEntry()
    {
        OwnChartWithMapping();
        OpenPeriod(new DateTime(2026, 10, 1));
        var service = Service();
        var sourceId = Guid.NewGuid();

        var a = await service.PostAsync(Invoice(sourceId, new DateTime(2026, 10, 5)));
        var b = await service.PostAsync(Invoice(sourceId, new DateTime(2026, 10, 5)));

        b.Should().BeSameAs(a);
        _added.Should().ContainSingle();
    }

    [Fact]
    public async Task ADocumentInAClosedPeriod_IsBookedTodayInTheCurrentOpenPeriod()
    {
        OwnChartWithMapping();
        OpenPeriod(new DateTime(2026, 7, 1), closed: true);
        var today = DateTime.UtcNow.Date;

        var entry = await Service().PostAsync(Invoice(Guid.NewGuid(), new DateTime(2026, 7, 15)));

        entry!.EntryDate.Should().Be(today);
        _addedPeriods.Should().ContainSingle("no period covered today, so the current month is opened");
        entry.FiscalPeriodId.Should().Be(_addedPeriods.Single().Id);
    }

    [Fact]
    public async Task WhenTodaysPeriodIsClosedToo_ThePostingIsRefused()
    {
        OwnChartWithMapping();
        OpenPeriod(new DateTime(2026, 7, 1), closed: true);
        OpenPeriod(DateTime.UtcNow.Date, closed: true);

        var act = () => Service().PostAsync(Invoice(Guid.NewGuid(), new DateTime(2026, 7, 15)));

        await act.Should().ThrowAsync<PostingConfigurationException>().WithMessage("*No open fiscal period*");
    }

    [Fact]
    public async Task ATenantWithItsOwnChartButNoMatchingAccount_NeedsConfiguration_NothingIsInvented()
    {
        _accounts.Add(NewAccount("111")); // its own chart, none of the default numbers

        var act = () => Service().PostAsync(Invoice(Guid.NewGuid(), new DateTime(2026, 10, 5)));

        var thrown = await act.Should().ThrowAsync<PostingConfigurationException>();
        thrown.Which.PostingKey.Should().NotBeNullOrEmpty();
        _addedAccounts.Should().BeEmpty("a tenant that has a chart is never given extra accounts");
    }

    [Fact]
    public async Task AMappedAccountThatIsInactive_IsRefused()
    {
        OwnChartWithMapping(receivableActive: false);
        OpenPeriod(new DateTime(2026, 10, 1));

        var act = () => Service().PostAsync(Invoice(Guid.NewGuid(), new DateTime(2026, 10, 5)));

        await act.Should().ThrowAsync<PostingConfigurationException>().WithMessage("*inactive*");
    }

    [Fact]
    public async Task WhenAutomaticPostingIsOff_NothingIsPosted()
    {
        var entry = await Service(enabled: false).PostAsync(Invoice(Guid.NewGuid(), DateTime.UtcNow));

        entry.Should().BeNull();
        _added.Should().BeEmpty();
        _addedAccounts.Should().BeEmpty();
    }

    [Fact]
    public async Task WithoutATenant_PostingIsRefused()
    {
        var service = Service(tenant: null);
        var tenantless = new Mock<ITenantProvider>();
        tenantless.SetupGet(t => t.CurrentTenantId).Returns((Guid?)null);
        var accounts = new Mock<IAccountingUnitOfWork>();
        var noTenant = new JournalPostingService(accounts.Object, Mock.Of<IJournalNumberGenerator>(), tenantless.Object,
            new ConfigurationBuilder().Build(), NullLogger<JournalPostingService>.Instance);

        var act = () => noTenant.PostAsync(Invoice(Guid.NewGuid(), DateTime.UtcNow));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tenant*");
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task Reverse_MirrorsThePostedEntry_OnceOnly()
    {
        OwnChartWithMapping();
        OpenPeriod(new DateTime(2026, 10, 1));
        var sourceId = Guid.NewGuid();
        var original = await Service().PostAsync(Invoice(sourceId, new DateTime(2026, 10, 5), 200m));
        _entries.Add(original!);
        _added.Clear();

        var service = Service();
        var reversal = await service.ReverseAsync("Invoice", sourceId, "Order cancelled", new DateTime(2026, 10, 6));
        var again = await service.ReverseAsync("Invoice", sourceId, "Order cancelled", new DateTime(2026, 10, 6));

        reversal!.SourceKey.Should().Be($"InvoiceReversal:{sourceId:N}");
        reversal.Lines.Single(l => l.Credit > 0).AccountId.Should().Be(original!.Lines.Single(l => l.Debit > 0).AccountId);
        original.Status.Should().Be((int)JournalStatus.Reversed);
        again.Should().BeSameAs(reversal);
        _added.Should().ContainSingle();
    }

    [Fact]
    public async Task Reverse_OfSomethingNeverPosted_DoesNothing()
    {
        var result = await Service().ReverseAsync("Invoice", Guid.NewGuid(), "x", DateTime.UtcNow);

        result.Should().BeNull();
        _added.Should().BeEmpty();
    }
}

internal static class JournalEntryTestExtensions
{
    public static string Number(this JournalEntry entry) => entry.EntryNumber;
}

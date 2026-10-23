using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Queries;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Handlers;

public class PostingProfileAndJournalHandlerTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private readonly Mock<IAccountingUnitOfWork> _uow = new();
    private readonly Mock<IAccountRepository> _accounts = new();
    private readonly Mock<IPostingAccountMappingRepository> _mappings = new();
    private readonly Mock<IJournalEntryRepository> _entries = new();
    private readonly Mock<IFiscalPeriodRepository> _periods = new();
    private readonly List<Account> _chart = new();
    private readonly List<PostingAccountMapping> _mapped = new();

    public PostingProfileAndJournalHandlerTests()
    {
        _uow.SetupGet(u => u.Accounts).Returns(_accounts.Object);
        _uow.SetupGet(u => u.PostingMappings).Returns(_mappings.Object);
        _uow.SetupGet(u => u.JournalEntries).Returns(_entries.Object);
        _uow.SetupGet(u => u.FiscalPeriods).Returns(_periods.Object);
        _accounts.Setup(a => a.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => _chart.Where(a => ids.Contains(a.Id)).ToList());
        _mappings.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _mapped.ToList());
    }

    private Account Account(string number, bool active = true)
    {
        var account = ERP.ACC.Domain.Entities.Account.Create(number, "A" + number, (int)AccountType.Asset, (int)NormalBalance.Debit);
        account.IsActive = active;
        _chart.Add(account);
        return account;
    }

    private UpdatePostingProfileCommandHandler UpdateHandler()
    {
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(t => t.CurrentTenantId).Returns(Tenant);
        return new UpdatePostingProfileCommandHandler(_uow.Object, tenant.Object);
    }

    private static UpdatePostingProfileCommand Update(params (string Key, Guid Account)[] items) =>
        new() { Mappings = items.Select(i => new PostingMappingInput { PostingKey = i.Key, AccountId = i.Account }).ToList() };

    [Fact]
    public async Task UpdatingTheProfile_PointsExistingKeys_AndAddsNewOnes()
    {
        var oldAr = Account("1100");
        var newAr = Account("131");
        var cash = Account("111");
        var existing = PostingAccountMapping.Create(Tenant, PostingKeys.AccountsReceivable, oldAr.Id);
        _mapped.Add(existing);
        PostingAccountMapping? added = null;
        _mappings.Setup(m => m.AddAsync(It.IsAny<PostingAccountMapping>(), It.IsAny<CancellationToken>()))
            .Callback<PostingAccountMapping, CancellationToken>((m, _) => added = m).Returns(Task.CompletedTask);

        await UpdateHandler().Handle(Update((PostingKeys.AccountsReceivable, newAr.Id), (PostingKeys.Cash, cash.Id)), default);

        existing.AccountId.Should().Be(newAr.Id);
        added!.PostingKey.Should().Be(PostingKeys.Cash);
        added.AccountId.Should().Be(cash.Id);
        added.TenantId.Should().Be(Tenant);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatingTheProfile_RefusesAnUnknownKey_AMissingAccount_AnInactiveOne_AndSharedArAp()
    {
        var good = Account("1100");
        var inactive = Account("9999", active: false);

        var unknownKey = () => UpdateHandler().Handle(Update(("Nonsense", good.Id)), default);
        var missing = () => UpdateHandler().Handle(Update((PostingKeys.Cash, Guid.NewGuid())), default);
        var inactiveAccount = () => UpdateHandler().Handle(Update((PostingKeys.Cash, inactive.Id)), default);
        var duplicateKey = () => UpdateHandler().Handle(Update((PostingKeys.Cash, good.Id), (PostingKeys.Cash, good.Id)), default);
        var sameArAp = () => UpdateHandler().Handle(
            Update((PostingKeys.AccountsReceivable, good.Id), (PostingKeys.AccountsPayable, good.Id)), default);

        await unknownKey.Should().ThrowAsync<BusinessRuleException>().WithMessage("*Unknown posting key*");
        await missing.Should().ThrowAsync<BusinessRuleException>().WithMessage("*does not exist*");
        await inactiveAccount.Should().ThrowAsync<BusinessRuleException>().WithMessage("*inactive*");
        await duplicateKey.Should().ThrowAsync<BusinessRuleException>().WithMessage("*only be mapped once*");
        await sameArAp.Should().ThrowAsync<BusinessRuleException>().WithMessage("*cannot be the same*");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheProfileQuery_ShowsEachKeyWithItsAccount()
    {
        var ar = Account("1100");
        _mapped.Add(PostingAccountMapping.Create(Tenant, PostingKeys.AccountsReceivable, ar.Id));

        var profile = await new GetPostingProfileQueryHandler(_uow.Object).Handle(new GetPostingProfileQuery(), default);

        var row = profile.Mappings.Should().ContainSingle().Subject;
        row.PostingKey.Should().Be(PostingKeys.AccountsReceivable);
        row.AccountNumber.Should().Be("1100");
        row.AccountName.Should().Be("A1100");
    }

    // ---- manual journal posting guards ----

    private PostJournalEntryCommandHandler PostHandler(out FiscalPeriod period)
    {
        period = FiscalPeriod.Create("October 2026", new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));
        var p = period;
        _periods.Setup(f => f.GetByIdAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
        return new PostJournalEntryCommandHandler(_uow.Object);
    }

    private static PostJournalEntryCommand Manual(Guid periodId, Guid a, Guid b, decimal debit = 10, decimal credit = 10, DateTime? date = null) =>
        new()
        {
            EntryNumber = "JE-M1", FiscalPeriodId = periodId, Description = "manual", EntryDate = date ?? new DateTime(2026, 10, 15),
            Lines = new List<JournalLineDto>
            {
                new() { AccountId = a, Debit = debit },
                new() { AccountId = b, Credit = credit }
            }
        };

    [Fact]
    public async Task AManualEntry_IsPostedWithItsDate_WhenEverythingIsValid()
    {
        var handler = PostHandler(out var period);
        var a = Account("1000"); var b = Account("4000");

        var result = await handler.Handle(Manual(period.Id, a.Id, b.Id), default);

        result.Status.Should().Be("Posted");
        result.EntryDate.Should().Be(new DateTime(2026, 10, 15));
        _entries.Verify(e => e.AddAsync(It.Is<JournalEntry>(j => j.Status == (int)JournalStatus.Posted), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AManualEntry_WithAnUnknownOrInactiveAccount_AOneSidedViolation_OrADateOutsideThePeriod_IsRefused()
    {
        var handler = PostHandler(out var period);
        var a = Account("1000"); var b = Account("4000"); var off = Account("9999", active: false);

        var unknown = () => handler.Handle(Manual(period.Id, a.Id, Guid.NewGuid()), default);
        var inactive = () => handler.Handle(Manual(period.Id, a.Id, off.Id), default);
        var bothSides = () => handler.Handle(new PostJournalEntryCommand
        {
            EntryNumber = "JE-M2", FiscalPeriodId = period.Id, Description = "x", EntryDate = new DateTime(2026, 10, 15),
            Lines = new List<JournalLineDto>
            {
                new() { AccountId = a.Id, Debit = 10, Credit = 10 }, new() { AccountId = b.Id, Debit = 0, Credit = 0 }
            }
        }, default);
        var wrongDate = () => handler.Handle(Manual(period.Id, a.Id, b.Id, date: new DateTime(2026, 11, 2)), default);

        await unknown.Should().ThrowAsync<BusinessRuleException>().WithMessage("*does not exist*");
        await inactive.Should().ThrowAsync<BusinessRuleException>().WithMessage("*inactive*");
        await bothSides.Should().ThrowAsync<BusinessRuleException>().WithMessage("*exactly one positive amount*");
        await wrongDate.Should().ThrowAsync<BusinessRuleException>().WithMessage("*outside fiscal period*");
        _entries.Verify(e => e.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- reversing an entry ----

    [Fact]
    public async Task ReversingAnEntry_NeedsAReason_APostedEntry_AndUsesThePostingService()
    {
        var a = Account("1000"); var b = Account("4000");
        var original = JournalEntry.CreatePosted("JE-1", Guid.NewGuid(), new DateTime(2026, 10, 5), "x", "Invoice", Guid.NewGuid(),
            new[] { new JournalLineDraft(a.Id, 10, 0), new JournalLineDraft(b.Id, 0, 10) });
        _entries.Setup(e => e.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        var mirror = original.Reverse("JE-2", original.FiscalPeriodId, DateTime.UtcNow, "test"); // a stand-in result
        var posting = new Mock<IJournalPostingService>();
        posting.Setup(p => p.ReverseEntryAsync(original, "Cancelled", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(mirror);
        var handler = new ReverseJournalEntryCommandHandler(_uow.Object, posting.Object);

        var noReason = () => handler.Handle(new ReverseJournalEntryCommand { JournalEntryId = original.Id, Reason = " " }, default);
        await noReason.Should().ThrowAsync<BusinessRuleException>().WithMessage("*reason*");

        // original is Reversed now (by the stand-in call above): a second reversal is refused
        var again = () => handler.Handle(new ReverseJournalEntryCommand { JournalEntryId = original.Id, Reason = "Cancelled" }, default);
        await again.Should().ThrowAsync<BusinessRuleException>().WithMessage("*not posted*");

        var missing = () => handler.Handle(new ReverseJournalEntryCommand { JournalEntryId = Guid.NewGuid(), Reason = "x" }, default);
        await missing.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ReversingAPostedEntry_SavesTheMirrorEntry()
    {
        var a = Account("1000"); var b = Account("4000");
        var original = JournalEntry.CreatePosted("JE-1", Guid.NewGuid(), new DateTime(2026, 10, 5), "x", "Invoice", Guid.NewGuid(),
            new[] { new JournalLineDraft(a.Id, 10, 0), new JournalLineDraft(b.Id, 0, 10) });
        _entries.Setup(e => e.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        var mirror = JournalEntry.CreatePosted("JE-2", original.FiscalPeriodId, DateTime.UtcNow, "Reversal", "InvoiceReversal", original.ReferenceId!.Value,
            new[] { new JournalLineDraft(a.Id, 0, 10), new JournalLineDraft(b.Id, 10, 0) });
        var posting = new Mock<IJournalPostingService>();
        posting.Setup(p => p.ReverseEntryAsync(original, "Cancelled", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(mirror);

        var result = await new ReverseJournalEntryCommandHandler(_uow.Object, posting.Object)
            .Handle(new ReverseJournalEntryCommand { JournalEntryId = original.Id, Reason = "Cancelled" }, default);

        result.EntryNumber.Should().Be("JE-2");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

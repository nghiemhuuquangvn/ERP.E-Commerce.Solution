using ERP.ACC.Application.Commands;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Handlers;

/// <summary>ROADMAP P1-2: void a hand-written entry of an open period; reverse any posted entry (optionally at a chosen date).</summary>
public class VoidAndReverseTests
{
    private readonly Mock<IAccountingUnitOfWork> _uow = new();
    private readonly Mock<IJournalEntryRepository> _entries = new();
    private readonly Mock<IFiscalPeriodRepository> _periods = new();

    public VoidAndReverseTests()
    {
        _uow.SetupGet(u => u.JournalEntries).Returns(_entries.Object);
        _uow.SetupGet(u => u.FiscalPeriods).Returns(_periods.Object);
    }

    private static JournalEntry Manual(Guid periodId)
    {
        var entry = JournalEntry.Create("JE-M1", periodId, "hand-written");
        entry.EntryDate = new DateTime(2026, 10, 5);
        entry.Post(10m, 10m);
        entry.AddLine(Guid.NewGuid(), 10m, 0m, "");
        entry.AddLine(Guid.NewGuid(), 0m, 10m, "");
        return entry;
    }

    private FiscalPeriod Period(bool closed = false)
    {
        var period = FiscalPeriod.Create("October 2026", new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));
        if (closed) period.Close();
        _periods.Setup(p => p.GetByIdAsync(period.Id, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        return period;
    }

    private Task<ERP.ACC.Application.DTOs.JournalEntryDto> Void(JournalEntry entry, string reason = "typed in twice")
    {
        _entries.Setup(e => e.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        return new VoidJournalEntryCommandHandler(_uow.Object).Handle(new VoidJournalEntryCommand { JournalEntryId = entry.Id, Reason = reason }, default);
    }

    [Fact]
    public async Task A_manual_entry_of_an_open_period_is_voided_with_its_reason_and_saved()
    {
        var entry = Manual(Period().Id);

        await Void(entry);

        entry.Status.Should().Be((int)JournalStatus.Voided);
        entry.VoidReason.Should().Be("typed in twice");
        entry.VoidedAt.Should().NotBeNull();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Voiding_needs_a_reason_an_open_period_a_manual_entry_and_never_touches_closing_entries()
    {
        var open = Period();
        var closed = Period(closed: true);

        await FluentActions.Awaiting(() => Void(Manual(open.Id), " ")).Should().ThrowAsync<BusinessRuleException>().WithMessage("*reason*");
        await FluentActions.Awaiting(() => Void(Manual(closed.Id))).Should().ThrowAsync<BusinessRuleException>().WithMessage("*closed*");

        var document = JournalEntry.CreatePosted("JE-2", open.Id, new DateTime(2026, 10, 5), "x", "Invoice", Guid.NewGuid(),
            new[] { new JournalLineDraft(Guid.NewGuid(), 10, 0), new JournalLineDraft(Guid.NewGuid(), 0, 10) });
        await FluentActions.Awaiting(() => Void(document)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*reverse it*");

        var closing = JournalEntry.CreatePosted("JE-3", open.Id, new DateTime(2026, 10, 31), "closing", JournalEntry.PeriodClosingType, Guid.NewGuid(),
            new[] { new JournalLineDraft(Guid.NewGuid(), 10, 0), new JournalLineDraft(Guid.NewGuid(), 0, 10) });
        await FluentActions.Awaiting(() => Void(closing)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*reopen*");

        var voided = Manual(open.Id);
        await Void(voided);
        await FluentActions.Awaiting(() => Void(voided)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*not posted*");
    }

    [Fact]
    public async Task Reversing_passes_the_chosen_date_and_refuses_a_closing_entry()
    {
        var period = Period();
        var original = JournalEntry.CreatePosted("JE-1", period.Id, new DateTime(2026, 10, 5), "x", "Invoice", Guid.NewGuid(),
            new[] { new JournalLineDraft(Guid.NewGuid(), 10, 0), new JournalLineDraft(Guid.NewGuid(), 0, 10) });
        var closing = JournalEntry.CreatePosted("JE-3", period.Id, new DateTime(2026, 10, 31), "closing", JournalEntry.PeriodClosingType, Guid.NewGuid(),
            new[] { new JournalLineDraft(Guid.NewGuid(), 10, 0), new JournalLineDraft(Guid.NewGuid(), 0, 10) });
        _entries.Setup(e => e.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        _entries.Setup(e => e.GetByIdAsync(closing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(closing);
        DateTime? usedDate = null;
        var posting = new Mock<IJournalPostingService>();
        posting.Setup(p => p.ReverseEntryAsync(original, "customer returned", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<JournalEntry, string, DateTime, CancellationToken>((_, _, d, _) => usedDate = d)
            .ReturnsAsync(JournalEntry.CreatePosted("JE-2", period.Id, new DateTime(2026, 10, 20), "Reversal", "InvoiceReversal", original.ReferenceId!.Value,
                new[] { new JournalLineDraft(Guid.NewGuid(), 0, 10), new JournalLineDraft(Guid.NewGuid(), 10, 0) }));
        var handler = new ReverseJournalEntryCommandHandler(_uow.Object, posting.Object);

        await handler.Handle(new ReverseJournalEntryCommand { JournalEntryId = original.Id, Reason = "customer returned", ReversalDate = new DateTime(2026, 10, 20, 15, 0, 0) }, default);

        usedDate.Should().Be(new DateTime(2026, 10, 20));
        await FluentActions.Awaiting(() => handler.Handle(new ReverseJournalEntryCommand { JournalEntryId = closing.Id, Reason = "x" }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*reopen*");
    }
}

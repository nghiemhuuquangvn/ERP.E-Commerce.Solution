using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-2l: every change of a note text is kept (who, when, what), per section, language and fiscal year.</summary>
public class NoteTextHistoryTests
{
    private readonly List<NoteText> _stored = new();
    private readonly List<NoteTextRevision> _revisions = new();
    private readonly Guid _tenant = Guid.NewGuid();

    private (UpdateNoteTextsCommandHandler Update, GetNoteTextHistoryQueryHandler History) Handlers(string user = "alice")
    {
        var repo = new Mock<INoteTextRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _stored.ToList());
        repo.Setup(r => r.AddAsync(It.IsAny<NoteText>(), It.IsAny<CancellationToken>())).Callback<NoteText, CancellationToken>((n, _) => _stored.Add(n)).Returns(Task.CompletedTask);
        repo.Setup(r => r.AddRevisionAsync(It.IsAny<NoteTextRevision>(), It.IsAny<CancellationToken>())).Callback<NoteTextRevision, CancellationToken>((r, _) => _revisions.Add(r)).Returns(Task.CompletedTask);
        repo.Setup(r => r.GetRevisionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, string lang, int year, int take, CancellationToken _) =>
                _revisions.Where(r => r.SectionKey == key && r.Language == lang && r.FiscalYear == year).OrderByDescending(r => r.CreatedAt).Take(take).ToList());
        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.NoteTexts).Returns(repo.Object);
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(t => t.CurrentTenantId).Returns(_tenant);
        return (new UpdateNoteTextsCommandHandler(uow.Object, tenant.Object, new UserContext { UserId = "u-1", UserName = user }), new GetNoteTextHistoryQueryHandler(repo.Object));
    }

    private static UpdateNoteTextsCommand Save(string text, int year = 2026, string lang = "vi", string key = "policy.cash") =>
        new() { Language = lang, FiscalYear = year, Texts = new() { [key] = text } };

    [Fact]
    public async Task A_new_text_and_each_change_are_recorded_with_the_user_and_an_identical_save_adds_nothing()
    {
        var (update, history) = Handlers();

        await update.Handle(Save("First."), default);
        await update.Handle(Save("First."), default);          // unchanged
        await update.Handle(Save("  Second.  "), default);

        _revisions.Select(r => r.Text).Should().Equal("First.", "Second.");
        _revisions.Should().OnlyContain(r => r.CreatedBy == "alice" && r.TenantId == _tenant);
        var shown = await history.Handle(new GetNoteTextHistoryQuery { Key = "policy.cash", Language = "vi", FiscalYear = 2026 }, default);
        shown.Select(h => h.Text).Should().Equal("Second.", "First.");        // newest first
    }

    [Fact]
    public async Task Clearing_a_text_is_recorded_as_an_empty_revision()
    {
        var (update, history) = Handlers();
        await update.Handle(Save("Text."), default);

        await update.Handle(Save(""), default);

        (await history.Handle(new GetNoteTextHistoryQuery { Key = "policy.cash", Language = "vi", FiscalYear = 2026 }, default)).First().Text.Should().BeEmpty();
    }

    [Fact]
    public async Task The_history_is_per_section_language_and_year()
    {
        var (update, history) = Handlers();
        await update.Handle(Save("vi 2026"), default);
        await update.Handle(Save("vi 2027", year: 2027), default);
        await update.Handle(Save("en 2026", lang: "en"), default);

        (await history.Handle(new GetNoteTextHistoryQuery { Key = "policy.cash", Language = "vi", FiscalYear = 2026 }, default)).Should().ContainSingle().Which.Text.Should().Be("vi 2026");
        (await history.Handle(new GetNoteTextHistoryQuery { Key = "policy.cash", Language = "en", FiscalYear = 2026 }, default)).Should().ContainSingle().Which.Text.Should().Be("en 2026");
        (await history.Handle(new GetNoteTextHistoryQuery { Key = "policy.fx", Language = "vi", FiscalYear = 2026 }, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task An_unknown_section_or_year_is_refused_and_the_list_is_capped()
    {
        var (update, history) = Handlers();

        var unknown = () => history.Handle(new GetNoteTextHistoryQuery { Key = "nope" }, default);
        var badYear = () => history.Handle(new GetNoteTextHistoryQuery { Key = "policy.cash", FiscalYear = -1 }, default);
        await unknown.Should().ThrowAsync<BusinessRuleException>();
        await badYear.Should().ThrowAsync<BusinessRuleException>();

        for (var i = 0; i < GetNoteTextHistoryQueryHandler.MaxRevisions + 5; i++)
            await update.Handle(Save($"version {i}"), default);
        (await history.Handle(new GetNoteTextHistoryQuery { Key = "policy.cash", FiscalYear = 2026 }, default)).Should().HaveCount(GetNoteTextHistoryQueryHandler.MaxRevisions);
    }
}

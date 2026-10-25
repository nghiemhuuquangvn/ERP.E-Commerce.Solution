using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using Moq;
using PdfSharp.Pdf.IO;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-2i: the notes to the financial statements B09-DN — figures from the books, texts from the accountant.</summary>
public class NotesTests
{
    private static readonly DateTime From = new(2026, 10, 1);
    private static readonly DateTime To = new(2026, 10, 31);

    private readonly List<Account> _chart = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();

    private Account A(string number) => _chart.Single(a => a.AccountNumber == number);

    private static AccountBalanceSummary B(Account a, decimal debit, decimal credit) => new() { AccountId = a.Id, TotalDebit = debit, TotalCredit = credit };

    private Dictionary<Guid, AccountBalanceSummary> Map(params (string Number, decimal Debit, decimal Credit)[] lines) =>
        lines.ToDictionary(l => A(l.Number).Id, l => B(A(l.Number), l.Debit, l.Credit));

    // a small consistent ledger: cash 1,000 → 1,300; receivables 0 → 300; equipment 500 → 800 with depreciation 100 → 150; payables 200 → 400; capital 1,500
    private (Dictionary<Guid, AccountBalanceSummary> Before, Dictionary<Guid, AccountBalanceSummary> After, Dictionary<Guid, AccountBalanceSummary> Activity, Dictionary<Guid, AccountBalanceSummary> Result) Ledger() => (
        Map(("1111", 1000m, 0m), ("211", 500m, 0m), ("2141", 0m, 100m), ("331", 0m, 200m), ("4111", 0m, 1400m)),
        Map(("1111", 1500m, 200m), ("131", 400m, 100m), ("211", 800m, 0m), ("2141", 0m, 150m), ("331", 100m, 500m), ("4111", 0m, 1400m)),
        Map(("1111", 500m, 200m), ("131", 400m, 100m), ("211", 300m, 0m), ("2141", 0m, 50m), ("331", 100m, 300m), ("5111", 0m, 1000m), ("632", 600m, 0m)),
        Map(("5111", 0m, 1000m), ("632", 600m, 0m)));

    private static decimal Cell(NoteTableDto t, string label, int column) => t.Rows.Single(r => r.Label.StartsWith(label)).Cells[column];

    // ---- the figures ----

    [Fact]
    public void A_movement_table_gives_opening_increase_decrease_and_closing_that_add_up()
    {
        var (before, after, activity, result) = Ledger();
        var ledger = new NoteLedger(_chart, before, after, activity, result, 1m);

        var table = ledger.Movement(new NoteLedger.Group("Cộng", new[] { "111" }, true));

        var cash = table.Rows.Single(r => r.Label.StartsWith("1111"));
        cash.Cells.Should().Equal(1000m, 500m, 200m, 1300m);
        (cash.Cells[0] + cash.Cells[1] - cash.Cells[2]).Should().Be(cash.Cells[3]);
        table.Rows.Last().IsTotal.Should().BeTrue();
        table.Rows.Last().Cells.Should().Equal(1000m, 500m, 200m, 1300m);
        table.Columns.Should().Equal(NoteLedger.MovementColumns);
    }

    [Fact]
    public void Accounts_with_nothing_to_show_are_left_out_and_a_contra_group_uses_its_own_side()
    {
        var (before, after, activity, result) = Ledger();
        var ledger = new NoteLedger(_chart, before, after, activity, result, 1m);

        var table = ledger.Movement(
            new NoteLedger.Group("Nguyên giá", new[] { "211", "212", "213" }, true),
            new NoteLedger.Group("Giá trị hao mòn lũy kế", new[] { "214" }, false));

        table.Rows.Select(r => r.Label).Should().Equal("211 Tài sản cố định hữu hình", "Nguyên giá", "2141 Hao mòn TSCĐ hữu hình", "Giá trị hao mòn lũy kế");
        NoteLedger.Subtotal(table, "Nguyên giá").Should().Equal(500m, 300m, 0m, 800m);
        NoteLedger.Subtotal(table, "Giá trị hao mòn lũy kế").Should().Equal(100m, 50m, 0m, 150m);   // credit-normal: shown positive
    }

    [Fact]
    public void Revenue_and_expense_tables_use_the_result_without_closing_entries()
    {
        var (before, after, activity, result) = Ledger();
        var ledger = new NoteLedger(_chart, before, after, activity, result, 1m);

        var revenue = ledger.Result("Cộng doanh thu", new[] { "511" }, debitNormal: false);
        var cost = ledger.Result("Cộng chi phí", new[] { "632" }, debitNormal: true);

        revenue.Rows.Last().Cells.Should().Equal(1000m);
        cost.Rows.Last().Cells.Should().Equal(600m);
        revenue.Rows.Should().HaveCount(2); // the account and the total
    }

    [Fact]
    public void Rounding_applies_row_by_row_in_the_chosen_unit_and_the_subtotal_adds_the_rounded_rows()
    {
        var before = Map(("1111", 1400m, 0m), ("1121", 1400m, 0m));
        var ledger = new NoteLedger(_chart, before, before, new Dictionary<Guid, AccountBalanceSummary>(), new Dictionary<Guid, AccountBalanceSummary>(), 1000m);

        var table = ledger.Movement(new NoteLedger.Group("Cộng", new[] { "111", "112" }, true));

        table.Rows.Where(r => !r.IsTotal).Select(r => r.Cells[0]).Should().Equal(1m, 1m);     // 1,400 → 1 each
        table.Rows.Last().Cells[0].Should().Be(2m);                                            // not round(2,800) = 3
    }

    // ---- the report ----

    private (GetNotesQueryHandler Handler, List<NoteText> Texts) Handler(
        List<Account> chart, (Dictionary<Guid, AccountBalanceSummary> Before, Dictionary<Guid, AccountBalanceSummary> After, Dictionary<Guid, AccountBalanceSummary> Activity, Dictionary<Guid, AccountBalanceSummary> Result) ledger,
        ReportProfile? profile = null, IEnumerable<Loan>? loans = null, IEnumerable<OffBalanceEntry>? offBalance = null, IEnumerable<NoteText>? texts = null, IEnumerable<LedgerRow>? journal = null)
    {
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => chart.ToList());
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(l => l.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime? f, DateTime? t, Guid? p, CancellationToken _) => (t == From.AddDays(-1) ? ledger.Before : ledger.After).Values.ToList());
        lines.Setup(l => l.GetActivityBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime? f, DateTime? t, string? exclude, CancellationToken _) => (exclude == null ? ledger.Activity : ledger.Result).Values.ToList());
        lines.Setup(l => l.GetLedgerLinesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((journal ?? Array.Empty<LedgerRow>()).ToList());
        var profiles = new Mock<IReportProfileRepository>();
        profiles.Setup(p => p.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        var policies = new Mock<IClosingPolicyRepository>();
        var inv = new Mock<IInvoiceRepository>();
        inv.Setup(r => r.GetUnpaidInvoicesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Invoice>());
        var bl = new Mock<IBillRepository>();
        bl.Setup(r => r.GetUnpaidBillsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Bill>());
        var ln = new Mock<ILoanRepository>();
        ln.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync((loans ?? Array.Empty<Loan>()).ToList());
        var off = new Mock<IOffBalanceRepository>();
        off.Setup(r => r.ListAsync(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>())).ReturnsAsync((offBalance ?? Array.Empty<OffBalanceEntry>()).ToList());
        var stored = (texts ?? Array.Empty<NoteText>()).ToList();
        var nt = new Mock<INoteTextRepository>();
        nt.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => stored.ToList());
        return (new GetNotesQueryHandler(accounts.Object, lines.Object, profiles.Object, policies.Object, inv.Object, bl.Object, ln.Object, off.Object, nt.Object, new Mock<IPaymentApplicationRepository>().Object, new Mock<IStatementMappingRepository>().Object), stored);
    }

    private static GetNotesQuery Query(string? unit = null) => new() { FromDate = From, ToDate = To, Unit = unit };

    private static NoteSectionDto Section(NotesDto n, string number) => n.Sections.Single(s => s.Number == number);

    [Fact]
    public async Task The_notes_have_the_sections_of_the_form_in_order_with_tables_from_the_books()
    {
        var notes = await Handler(_chart, Ledger()).Handler.Handle(Query(), default);

        notes.Sections.Select(s => s.Number).Should().Equal(
            "I", "II", "III", "IV.1", "IV.2", "IV.3", "IV.4", "IV.5", "IV.6", "IV.7", "IV.8",
            "V.1", "V.2", "V.3", "V.4", "V.5", "V.6", "V.7", "V.8", "V.9", "V.10", "V.11", "V.12", "VI.1", "VI.2", "VII", "VIII.1", "VIII.2");
        Cell(Section(notes, "V.1").Table!, "Cộng", 3).Should().Be(1300m);
        Cell(Section(notes, "V.2").Table!, "131", 3).Should().Be(300m);
        var fixedAssets = Section(notes, "V.4").Table!;
        Cell(fixedAssets, "Giá trị còn lại", 0).Should().Be(400m);        // 500 − 100 at the start
        Cell(fixedAssets, "Giá trị còn lại", 3).Should().Be(650m);        // 800 − 150 at the end
        Cell(Section(notes, "V.7").Table!, "331", 3).Should().Be(400m);
        Cell(Section(notes, "VI.1").Table!, "Cộng doanh thu", 0).Should().Be(1000m);
        Cell(Section(notes, "VI.2").Table!, "Cộng giá vốn", 0).Should().Be(600m);
        notes.IsDraft.Should().BeTrue();
        notes.Notes.Should().Contain(n => n.StartsWith("Draft"));
    }

    [Fact]
    public async Task The_closing_cash_in_the_notes_equals_the_balance_sheet_cash()
    {
        var ledger = Ledger();
        var notes = await Handler(_chart, ledger).Handler.Handle(Query(), default);
        var b01 = StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.BalanceSheet)!, _chart, ledger.After, null);

        Cell(Section(notes, "V.1").Table!, "Cộng", 3).Should().Be(b01.Rows.Single(r => r.Code == "110").Amount);
        Cell(Section(notes, "V.2").Table!, "Cộng", 3).Should().Be(b01.Rows.Single(r => r.Code == "131").Amount + b01.Rows.Single(r => r.Code == "211").Amount);
        Cell(Section(notes, "V.7").Table!, "Cộng", 3).Should().Be(b01.Rows.Single(r => r.Code == "311").Amount + b01.Rows.Single(r => r.Code == "331").Amount);
    }

    [Fact]
    public async Task The_texts_of_the_accountant_and_the_company_block_fill_the_text_sections_and_empty_ones_show_a_hint()
    {
        var profile = ReportProfile.Create(Guid.NewGuid());
        profile.Set("Công ty TNHH Mẫu", "0312345678", "1 Lê Lợi", "A", "B", "C", AmountUnit.Vnd);
        var texts = new[]
        {
            NoteText.Create(Guid.NewGuid(), "general", "Công ty thương mại bán lẻ."),
            NoteText.Create(Guid.NewGuid(), "policy.inventory", "Giá xuất kho theo bình quân gia quyền."),
        };

        var notes = await Handler(_chart, Ledger(), profile, texts: texts).Handler.Handle(Query(), default);

        var general = Section(notes, "I");
        general.Text.Should().Contain("Tên doanh nghiệp: Công ty TNHH Mẫu").And.Contain("Mã số thuế: 0312345678").And.EndWith("Công ty thương mại bán lẻ.");
        general.Hint.Should().BeNull();
        Section(notes, "IV.3").Text.Should().Be("Giá xuất kho theo bình quân gia quyền.");
        var empty = Section(notes, "IV.2");
        empty.Text.Should().BeNull();
        empty.Hint.Should().NotBeNullOrEmpty();
        Section(notes, "II").Text.Should().Contain("từ 01/10/2026 đến 31/10/2026").And.Contain("đồng Việt Nam").And.Contain("tháng 12");
        Section(notes, "II").IsGenerated.Should().BeTrue();
        notes.Header!.ChiefAccountant.Should().Be("B");
    }

    [Fact]
    public async Task Borrowings_come_from_the_loan_ledger_with_the_split_and_the_ledger_balance()
    {
        var loan = Loan.Create("LN-1", "Vietcombank", 3000m, 8m, new DateTime(2026, 1, 1),
            new[] { (new DateTime(2027, 1, 15), 1000m, 0m), (new DateTime(2028, 1, 15), 1000m, 0m), (new DateTime(2029, 1, 15), 1000m, 0m) }, null, null);
        var ledger = Ledger();
        var table = (await Handler(_chart, (ledger.Before, Map(("341", 0m, 3000m)), ledger.Activity, ledger.Result), loans: new[] { loan }).Handler.Handle(Query(), default)).Sections.Single(s => s.Number == "V.10").Table!;

        table.Rows.First().Label.Should().Be("LN-1 — Vietcombank");
        table.Rows.First().Cells.Should().Equal(3000m, 3000m, 1000m, 2000m);   // principal, outstanding, within 12 months, later
        table.Rows.Single(r => r.Label == "Cộng").Cells.Should().Equal(3000m, 3000m, 1000m, 2000m);
        Cell(table, "Số dư trên sổ cái", 1).Should().Be(3000m);
    }

    [Fact]
    public async Task The_off_balance_register_is_a_section_of_the_notes()
    {
        var chart = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();
        var account = chart.Single(a => a.AccountNumber == "001");
        var entries = new[]
        {
            OffBalanceEntry.Create(account.Id, new DateTime(2026, 9, 5), OffBalanceDirection.In, 1000m, "x", null),
            OffBalanceEntry.Create(account.Id, new DateTime(2026, 10, 9), OffBalanceDirection.Out, 250m, "y", null),
        };

        var table = (await Handler(chart, Ledger(), offBalance: entries).Handler.Handle(Query(), default)).Sections.Single(s => s.Number == "V.12").Table!;

        table.Rows.Single().Cells.Should().Equal(1000m, 0m, 250m, 750m);
    }

    [Fact]
    public async Task The_unit_comes_from_the_request_or_the_profile_and_a_wrong_range_or_unit_is_refused()
    {
        var handler = Handler(_chart, Ledger()).Handler;

        var thousand = await handler.Handle(Query("thousand"), default);
        thousand.Unit.Should().Be("thousand VND");
        Cell(Section(thousand, "V.1").Table!, "Cộng", 3).Should().Be(1m);      // 1,300 → 1
        Section(thousand, "II").Text.Should().Contain("nghìn đồng");

        await FluentActions.Awaiting(() => handler.Handle(new GetNotesQuery { FromDate = To, ToDate = From }, default)).Should().ThrowAsync<BusinessRuleException>();
        await FluentActions.Awaiting(() => handler.Handle(Query("dong"), default)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*Vnd, Thousand or Million*");
    }

    [Fact]
    public async Task A_14_account_tenant_gets_only_the_tables_its_chart_has()
    {
        var s14 = ChartTemplates.Find(ChartTemplates.Simple14)!.BuildAccounts();
        Dictionary<Guid, AccountBalanceSummary> M(params (string N, decimal D, decimal C)[] l) =>
            l.ToDictionary(x => s14.Single(a => a.AccountNumber == x.N).Id, x => new AccountBalanceSummary { AccountId = s14.Single(a => a.AccountNumber == x.N).Id, TotalDebit = x.D, TotalCredit = x.C });
        var ledger = (M(("1000", 100m, 0m), ("3000", 0m, 100m)), M(("1000", 600m, 0m), ("3000", 0m, 100m), ("4000", 0m, 500m)), M(("1000", 500m, 0m), ("4000", 0m, 500m)), M(("4000", 0m, 500m)));

        var notes = await Handler(s14, ledger).Handler.Handle(Query(), default);

        Cell(Section(notes, "V.1").Table!, "Cộng", 3).Should().Be(600m);
        Cell(Section(notes, "VI.1").Table!, "Cộng doanh thu", 0).Should().Be(500m);
        notes.Sections.Select(s => s.Number).Should().Contain("V.3").And.NotContain(new[] { "V.5", "V.6" });   // inventory (1200) exists; construction in progress and prepaid expenses do not
        notes.Notes.Should().Contain(n => n.Contains("14-account"));
    }

    // ---- the texts ----

    private (UpdateNoteTextsCommandHandler Handler, List<NoteText> Stored, Mock<IAccountingUnitOfWork> Uow) Texts()
    {
        var stored = new List<NoteText>();
        var repo = new Mock<INoteTextRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => stored.ToList());
        repo.Setup(r => r.AddAsync(It.IsAny<NoteText>(), It.IsAny<CancellationToken>())).Callback<NoteText, CancellationToken>((n, _) => stored.Add(n)).Returns(Task.CompletedTask);
        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.NoteTexts).Returns(repo.Object);
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(t => t.CurrentTenantId).Returns(Guid.NewGuid());
        return (new UpdateNoteTextsCommandHandler(uow.Object, tenant.Object), stored, uow);
    }

    [Fact]
    public async Task Texts_are_created_updated_and_cleared_by_key_and_unknown_keys_or_long_texts_are_refused()
    {
        var (handler, stored, uow) = Texts();

        var saved = await handler.Handle(new UpdateNoteTextsCommand { Texts = new() { ["policy.cash"] = "  Tiền gồm tiền mặt và tiền gửi.  ", ["policy.fx"] = "" } }, default);

        stored.Should().ContainSingle().Which.Text.Should().Be("Tiền gồm tiền mặt và tiền gửi.");   // trimmed; the empty one is not stored
        saved.Should().HaveCount(NoteTextSections.All.Count);
        saved.Single(s => s.Key == "policy.cash").Text.Should().Contain("tiền mặt");

        await handler.Handle(new UpdateNoteTextsCommand { Texts = new() { ["policy.cash"] = "Mới" } }, default);
        stored.Single().Text.Should().Be("Mới");
        await handler.Handle(new UpdateNoteTextsCommand { Texts = new() { ["policy.cash"] = "" } }, default);
        stored.Single().Text.Should().BeEmpty();

        await FluentActions.Awaiting(() => handler.Handle(new UpdateNoteTextsCommand { Texts = new() { ["nonsense"] = "x" } }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*Unknown note section*");
        await FluentActions.Awaiting(() => handler.Handle(new UpdateNoteTextsCommand { Texts = new() { ["policy.tax"] = new string('x', 4001) } }, default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*4000*");
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public void Every_text_key_is_unique_and_has_a_number_a_title_and_a_hint()
    {
        NoteTextSections.All.Select(s => s.Key).Should().OnlyHaveUniqueItems();
        NoteTextSections.All.Select(s => s.Number).Should().OnlyHaveUniqueItems();
        NoteTextSections.All.Should().OnlyContain(s => s.Number.Length > 0 && s.Title.Length > 0 && s.Hint.Length > 0 && s.Key.Length <= NoteText.MaxKeyLength);
    }

    // ---- the files ----

    private async Task<NotesDto> SampleNotes()
    {
        var profile = ReportProfile.Create(Guid.NewGuid());
        profile.Set("Công ty TNHH Mẫu", "0312345678", "1 Lê Lợi", "Nguyễn A", "Trần B", "Lê C", AmountUnit.Vnd);
        return await Handler(_chart, Ledger(), profile, texts: new[] { NoteText.Create(Guid.NewGuid(), "policy.cash", "Tiền gồm tiền mặt.") }).Handler.Handle(Query(), default);
    }

    [Fact]
    public async Task The_csv_and_excel_table_has_the_company_block_the_sections_and_the_signatories()
    {
        var table = ReportTables.Notes(await SampleNotes());
        var texts = table.Rows.SelectMany(r => r).OfType<string>().ToList();

        texts.Should().Contain("Công ty TNHH Mẫu").And.Contain("Tiền gồm tiền mặt.").And.Contain("Tiền và các khoản tương đương tiền").And.Contain("Nguyễn A");
        table.Rows.Any(r => r.OfType<decimal>().Contains(1300m)).Should().BeTrue();
        ERP.ACC.Application.Reports.XlsxWriter.ToBytes(table).Length.Should().BeGreaterThan(1000);
    }

    private static IEnumerable<string> Texts(DocumentObject? node)
    {
        switch (node)
        {
            case Paragraph p:
                yield return string.Concat(p.Elements.OfType<Text>().Select(t => t.Content));
                break;
            case Table table:
                foreach (Row row in table.Rows)
                    foreach (Cell cell in row.Cells)
                        foreach (var t in Texts(cell.Elements))
                            yield return t;
                break;
            case DocumentElements elements:
                foreach (DocumentObject child in elements)
                    foreach (var t in Texts(child))
                        yield return t;
                break;
            case Section section:
                foreach (var t in Texts(section.Elements))
                    yield return t;
                break;
        }
    }

    [Fact]
    public async Task The_pdf_renders_with_vietnamese_section_titles_tables_and_signatories()
    {
        var notes = await SampleNotes();
        var text = PdfWriter.Build(notes).Sections.Cast<Section>().SelectMany(s => Texts(s)).ToList();

        text.Should().Contain("Công ty TNHH Mẫu");
        text.Should().Contain("I. Đặc điểm hoạt động của doanh nghiệp");
        text.Should().Contain("V.4. Tài sản cố định và bất động sản đầu tư (nguyên giá, hao mòn, giá trị còn lại)");
        text.Should().Contain("Tiền gồm tiền mặt.");
        text.Should().Contain(t => t.StartsWith("(Chưa có nội dung")); // a section without text shows its hint
        text.Should().Contain("(Không phát sinh số liệu trong kỳ)");     // a table with no rows says so instead of printing an empty frame
        text.Should().Contain("1.300").And.Contain("Giá trị còn lại").And.Contain("Kế toán trưởng").And.Contain("Trần B");

        var bytes = PdfWriter.ToBytes(notes);
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        using var stream = new MemoryStream(bytes);
        PdfReader.Open(stream, PdfDocumentOpenMode.Import).PageCount.Should().BeGreaterThanOrEqualTo(2);
    }

    // ---- ROADMAP P1-2j: English, per-language texts, non-cash transactions, the prior period ----

    private static readonly System.Text.RegularExpressions.Regex Vietnamese = new("[àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static GetNotesQuery EnglishQuery() => new() { FromDate = From, ToDate = To, Language = "en" };

    [Fact]
    public async Task The_english_notes_use_english_titles_columns_and_labels_everywhere_the_fixed_wording_appears()
    {
        var notes = await Handler(_chart, Ledger()).Handler.Handle(EnglishQuery(), default);

        notes.Language.Should().Be("en");
        notes.Title.Should().StartWith("Notes to the financial statements");
        Section(notes, "I").Title.Should().Be("Nature of operations");
        Section(notes, "V.4").Title.Should().Contain("Fixed assets");
        Section(notes, "V.1").Table!.Columns.Should().Equal("Opening", "Increase", "Decrease", "Closing");
        Cell(Section(notes, "V.1").Table!, "Total", 3).Should().Be(1300m);
        Cell(Section(notes, "V.4").Table!, "Net book value", 3).Should().Be(650m);
        Cell(Section(notes, "VI.1").Table!, "Total revenue", 0).Should().Be(1000m);
        Section(notes, "II").Text.Should().Contain("Reporting period").And.Contain("Unit of the report: dong");

        // no Vietnamese in the fixed wording (account names come from the chart and stay as they are)
        var wording = notes.Sections.SelectMany(s => new[] { s.Title, s.Hint, s.Text }.Concat(s.Table?.Columns ?? new List<string>())
            .Concat((s.Table?.Rows ?? new()).Where(r => r.IsTotal || r.Label.StartsWith("Of which") || r.Label.StartsWith("Net")).Select(r => r.Label)))
            .Where(x => x != null).ToList();
        wording.Should().NotContain(x => Vietnamese.IsMatch(x!));
    }

    [Fact]
    public async Task The_texts_are_kept_per_language_and_the_other_language_shows_its_hint()
    {
        var texts = new[] { NoteText.Create(Guid.NewGuid(), "policy.cash", "Tiền gồm tiền mặt."), NoteText.Create(Guid.NewGuid(), "policy.cash", "Cash is cash on hand.", "en") };
        var h = Handler(_chart, Ledger(), texts: texts).Handler;

        var vi = await h.Handle(Query(), default);
        var en = await h.Handle(EnglishQuery(), default);

        Section(vi, "IV.1").Text.Should().Be("Tiền gồm tiền mặt.");
        Section(en, "IV.1").Text.Should().Be("Cash is cash on hand.");
        Section(en, "IV.2").Text.Should().BeNull();
        Section(en, "IV.2").Hint.Should().Contain("provision for doubtful debts");
    }

    [Fact]
    public async Task Saving_and_listing_the_texts_work_per_language_and_an_unknown_language_is_refused()
    {
        var (handler, stored, uow) = Texts();

        await handler.Handle(new UpdateNoteTextsCommand { Language = "en", Texts = new() { ["policy.fx"] = "Rates of the State Bank." } }, default);
        await handler.Handle(new UpdateNoteTextsCommand { Texts = new() { ["policy.fx"] = "Tỷ giá Ngân hàng Nhà nước." } }, default);

        stored.Should().HaveCount(2);
        stored.Select(n => n.Language).Should().BeEquivalentTo("en", "vi");
        var repo = uow.Object.NoteTexts;
        var en = await new GetNoteTextsQueryHandler(repo).Handle(new GetNoteTextsQuery { Language = "en" }, default);
        en.Single(x => x.Key == "policy.fx").Text.Should().Be("Rates of the State Bank.");
        en.Single(x => x.Key == "policy.fx").Title.Should().Be("Accounting policy: foreign currency");
        await FluentActions.Awaiting(() => handler.Handle(new UpdateNoteTextsCommand { Language = "fr", Texts = new() }, default)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*Unsupported language*");
        await FluentActions.Awaiting(() => Handler(_chart, Ledger()).Handler.Handle(new GetNotesQuery { FromDate = From, ToDate = To, Language = "de" }, default)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task A_comparison_period_adds_a_prior_column_to_the_movement_and_result_tables()
    {
        var q = Query();
        q.CompareFromDate = new DateTime(2026, 9, 1);
        q.CompareToDate = new DateTime(2026, 9, 30);

        var notes = await Handler(_chart, Ledger()).Handler.Handle(q, default);

        var cash = Section(notes, "V.1").Table!;
        cash.Columns.Should().HaveCount(5).And.EndWith("Số cuối kỳ trước");
        cash.Rows.Single(r => r.IsTotal).Cells.Should().HaveCount(5);
        cash.Rows.Single(r => r.IsTotal).Cells[3].Should().Be(1300m);   // closing now
        cash.Rows.Single(r => r.IsTotal).Cells[4].Should().Be(1000m);   // closing of the prior period (the ledger the day before October)
        var revenue = Section(notes, "VI.1").Table!;
        revenue.Columns.Should().Equal("Số tiền", "Kỳ trước");
        notes.CompareFromDate.Should().Be(new DateTime(2026, 9, 1));
    }

    [Fact]
    public async Task A_half_given_or_reversed_comparison_period_is_refused()
    {
        var h = Handler(_chart, Ledger()).Handler;
        var half = Query();
        half.CompareFromDate = new DateTime(2026, 9, 1);
        var reversed = Query();
        reversed.CompareFromDate = new DateTime(2026, 9, 30);
        reversed.CompareToDate = new DateTime(2026, 9, 1);

        await FluentActions.Awaiting(() => h.Handle(half, default)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*both dates*");
        await FluentActions.Awaiting(() => h.Handle(reversed, default)).Should().ThrowAsync<BusinessRuleException>().WithMessage("*after*");
    }

    private LedgerRow Row(Guid entry, string account, decimal debit, decimal credit, string? type = null) =>
        new() { EntryId = entry, EntryNumber = "E", EntryDate = From.AddDays(3), AccountId = A(account).Id, Debit = debit, Credit = credit, ReferenceType = type };

    [Fact]
    public async Task Non_cash_transactions_are_found_in_the_journal_and_cash_entries_and_closing_entries_are_left_out()
    {
        _chart.Single(a => a.AccountNumber == "1111").SetCashFlow(true, null);
        var onCredit = Guid.NewGuid();
        var toEquity = Guid.NewGuid();
        var paid = Guid.NewGuid();
        var closing = Guid.NewGuid();
        var journal = new[]
        {
            Row(onCredit, "211", 300m, 0m), Row(onCredit, "331", 0m, 300m),                       // equipment bought on credit
            Row(toEquity, "331", 200m, 0m), Row(toEquity, "4111", 0m, 200m),                       // supplier debt turned into capital
            Row(paid, "211", 100m, 0m), Row(paid, "1111", 0m, 100m),                               // paid in cash: not here
            Row(closing, "331", 900m, 0m, JournalEntry.PeriodClosingType), Row(closing, "4111", 0m, 900m, JournalEntry.PeriodClosingType),
        };

        var notes = await Handler(_chart, Ledger(), journal: journal).Handler.Handle(EnglishQuery(), default);

        var table = Section(notes, "VII").Table!;
        table.Rows.Select(r => r.Label).Should().Equal("Fixed assets acquired on credit", "Debt converted into equity", "Total");
        table.Rows.Select(r => r.Cells[0]).Should().Equal(300m, 200m, 500m);
        notes.Notes.Should().Contain(n => n.Contains("non-cash table"));
    }

    [Fact]
    public async Task Without_non_cash_entries_section_VII_stays_a_text_with_its_hint()
    {
        var notes = await Handler(_chart, Ledger()).Handler.Handle(Query(), default);

        Section(notes, "VII").Table.Should().BeNull();
        Section(notes, "VII").Hint.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task The_english_pdf_and_export_use_english_labels_and_number_format()
    {
        var profile = ReportProfile.Create(Guid.NewGuid());
        profile.Set("Mau Co Ltd", "0312345678", "1 Le Loi", "Nguyen A", "Tran B", "Le C", AmountUnit.Vnd);
        var notes = await Handler(_chart, Ledger(), profile).Handler.Handle(EnglishQuery(), default);

        var text = PdfWriter.Build(notes).Sections.Cast<Section>().SelectMany(s => Texts(s)).ToList();

        text.Should().Contain("Tax code: 0312345678").And.Contain("I. Nature of operations").And.Contain("Item").And.Contain("Chief accountant").And.Contain("Tran B");
        text.Should().Contain("1,300");
        text.Should().Contain(t => t.StartsWith("From 01/10/2026 to 31/10/2026") && t.Contains("Unit: dong"));
        text.Should().Contain(t => t.StartsWith("(Not written yet"));
        PdfWriter.ToBytes(notes).Length.Should().BeGreaterThan(1000);
        ReportTables.Notes(notes).Rows.SelectMany(r => r).OfType<string>().Should().Contain("en");
    }

    // ---- ROADMAP P1-2k: texts per fiscal year ----

    private static NoteText Txt(string key, string text, int year, string lang = "vi") => NoteText.Create(Guid.NewGuid(), key, text, lang, year);

    [Fact]
    public void A_report_reads_the_text_of_its_year_else_the_latest_earlier_year_else_the_texts_for_every_year()
    {
        var stored = new[] { Txt("policy.cash", "every year", 0), Txt("policy.cash", "2025", 2025), Txt("policy.cash", "2027", 2027), Txt("policy.fx", "only 2026", 2026) };

        NoteTextVersions.Resolve(stored, "vi", 2027)["policy.cash"].Should().Be(("2027", 2027));
        NoteTextVersions.Resolve(stored, "vi", 2026)["policy.cash"].Should().Be(("2025", 2025));      // carried forward
        NoteTextVersions.Resolve(stored, "vi", 2024)["policy.cash"].Should().Be(("every year", 0));    // before any version
        NoteTextVersions.Resolve(stored, "vi", 2025).Should().NotContainKey("policy.fx");             // written for a later year
        NoteTextVersions.Resolve(stored, "en", 2027).Should().BeEmpty();                               // another language
    }

    [Fact]
    public void A_cleared_year_falls_back_to_the_earlier_text()
    {
        var cleared = Txt("policy.cash", "2026", 2026);
        cleared.Set("");

        NoteTextVersions.Resolve(new[] { Txt("policy.cash", "2025", 2025), cleared }, "vi", 2026)["policy.cash"].Should().Be(("2025", 2025));
    }

    [Theory]
    [InlineData("2026-12-31", 12, 2026)]
    [InlineData("2026-06-30", 12, 2026)]
    [InlineData("2026-03-31", 3, 2026)]    // a year ending in March: the year that ends 31 Mar 2026
    [InlineData("2026-04-01", 3, 2027)]
    [InlineData("2026-10-15", 6, 2027)]
    public void The_fiscal_year_is_the_year_in_which_it_ends(string date, int yearEndMonth, int expected)
    {
        NoteTextVersions.FiscalYearOf(DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture), yearEndMonth).Should().Be(expected);
    }

    [Fact]
    public async Task Saving_a_year_does_not_touch_another_and_the_listing_marks_inherited_texts()
    {
        var (handler, stored, uow) = Texts();

        await handler.Handle(new UpdateNoteTextsCommand { FiscalYear = 2025, Texts = new() { ["policy.cash"] = "Policy 2025" } }, default);
        await handler.Handle(new UpdateNoteTextsCommand { FiscalYear = 2027, Texts = new() { ["policy.cash"] = "Policy 2027" } }, default);

        stored.Select(n => (n.FiscalYear, n.Text)).Should().BeEquivalentTo(new[] { (2025, "Policy 2025"), (2027, "Policy 2027") });
        var list = new GetNoteTextsQueryHandler(uow.Object.NoteTexts);
        var y2026 = (await list.Handle(new GetNoteTextsQuery { FiscalYear = 2026 }, default)).Single(x => x.Key == "policy.cash");
        y2026.Text.Should().Be("Policy 2025");
        y2026.Inherited.Should().BeTrue();
        y2026.SourceYear.Should().Be(2025);
        var y2027 = (await list.Handle(new GetNoteTextsQuery { FiscalYear = 2027 }, default)).Single(x => x.Key == "policy.cash");
        y2027.Inherited.Should().BeFalse();

        await handler.Handle(new UpdateNoteTextsCommand { FiscalYear = 2027, Texts = new() { ["policy.cash"] = "" } }, default);   // clear 2027
        (await list.Handle(new GetNoteTextsQuery { FiscalYear = 2027 }, default)).Single(x => x.Key == "policy.cash").Text.Should().Be("Policy 2025");
        await FluentActions.Awaiting(() => handler.Handle(new UpdateNoteTextsCommand { FiscalYear = -1, Texts = new() }, default)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task The_notes_use_the_texts_of_the_fiscal_year_of_the_end_date()
    {
        var texts = new[] { Txt("policy.cash", "Policy 2025", 2025), Txt("policy.cash", "Policy 2026", 2026), Txt("policy.cash", "Policy 2027", 2027) };
        var h = Handler(_chart, Ledger(), texts: texts).Handler;

        var notes = await h.Handle(Query(), default);                                                                // October 2026 → fiscal year 2026
        var earlier = await h.Handle(new GetNotesQuery { FromDate = new DateTime(2025, 10, 1), ToDate = new DateTime(2025, 10, 31) }, default);

        notes.FiscalYear.Should().Be(2026);
        Section(notes, "IV.1").Text.Should().Be("Policy 2026");
        Section(earlier, "IV.1").Text.Should().Be("Policy 2025");
    }
}

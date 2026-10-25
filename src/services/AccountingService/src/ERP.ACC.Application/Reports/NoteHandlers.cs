using ERP.ACC.Application.DTOs;
using System.Globalization;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Reports
{
    /// <summary>The notes B09-DN for a range (ROADMAP P1-2i): texts written by the accountant plus tables computed from the books.</summary>
    public class GetNotesQuery : IRequest<NotesDto>
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        /// <summary>"Vnd", "Thousand" or "Million"; empty = the report profile's unit.</summary>
        public string? Unit { get; set; }
    }

    public class NoteTextEntryDto
    {
        public string Key { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class GetNoteTextsQuery : IRequest<List<NoteTextEntryDto>> { }

    /// <summary>Saves the texts of the given keys; an empty text clears it. Keys that are not note sections are refused.</summary>
    public class UpdateNoteTextsCommand : IRequest<List<NoteTextEntryDto>>
    {
        public Dictionary<string, string> Texts { get; set; } = new();
    }

    public class GetNoteTextsQueryHandler : IRequestHandler<GetNoteTextsQuery, List<NoteTextEntryDto>>
    {
        private readonly INoteTextRepository _texts;

        public GetNoteTextsQueryHandler(INoteTextRepository texts) => _texts = texts;

        public async Task<List<NoteTextEntryDto>> Handle(GetNoteTextsQuery request, CancellationToken cancellationToken) =>
            NoteTextMapper.Map(await _texts.GetAllAsync(cancellationToken));
    }

    internal static class NoteTextMapper
    {
        public static List<NoteTextEntryDto> Map(IEnumerable<NoteText> stored)
        {
            var byKey = stored.ToDictionary(n => n.SectionKey, n => n.Text);
            return NoteTextSections.All.Select(s => new NoteTextEntryDto
            {
                Key = s.Key, Number = s.Number, Title = s.Title, Hint = s.Hint, Text = byKey.GetValueOrDefault(s.Key, string.Empty),
            }).ToList();
        }
    }

    public class UpdateNoteTextsCommandHandler : IRequestHandler<UpdateNoteTextsCommand, List<NoteTextEntryDto>>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly ITenantProvider _tenant;

        public UpdateNoteTextsCommandHandler(IAccountingUnitOfWork uow, ITenantProvider tenant)
        {
            _uow = uow;
            _tenant = tenant;
        }

        public async Task<List<NoteTextEntryDto>> Handle(UpdateNoteTextsCommand request, CancellationToken cancellationToken)
        {
            var unknown = request.Texts.Keys.Where(k => NoteTextSections.Find(k) == null).ToList();
            if (unknown.Count > 0)
                throw new BusinessRuleException("Accounting", $"Unknown note section(s): {string.Join(", ", unknown)}.");

            var stored = await _uow.NoteTexts.GetAllAsync(cancellationToken);
            foreach (var (key, text) in request.Texts)
            {
                var existing = stored.FirstOrDefault(n => n.SectionKey == key);
                try
                {
                    if (existing != null)
                        existing.Set(text);
                    else if (!string.IsNullOrWhiteSpace(text))
                    {
                        var tenantId = _tenant.CurrentTenantId ?? throw new BusinessRuleException("Accounting", "No tenant in the request.");
                        var created = NoteText.Create(tenantId, key, text);
                        await _uow.NoteTexts.AddAsync(created, cancellationToken);
                        stored.Add(created);
                    }
                }
                catch (ArgumentException ex)
                {
                    throw new BusinessRuleException("Accounting", ex.Message);
                }
            }

            await _uow.SaveChangesAsync(cancellationToken);
            return NoteTextMapper.Map(stored);
        }
    }

    public class GetNotesQueryHandler : IRequestHandler<GetNotesQuery, NotesDto>
    {
        private readonly IAccountRepository _accounts;
        private readonly IJournalLineRepository _lines;
        private readonly IReportProfileRepository _profiles;
        private readonly IClosingPolicyRepository _policies;
        private readonly IInvoiceRepository _invoices;
        private readonly IBillRepository _bills;
        private readonly IPaymentApplicationRepository _payments;
        private readonly ILoanRepository _loans;
        private readonly IOffBalanceRepository _offBalance;
        private readonly INoteTextRepository _texts;

        public GetNotesQueryHandler(
            IAccountRepository accounts, IJournalLineRepository lines, IReportProfileRepository profiles, IClosingPolicyRepository policies,
            IInvoiceRepository invoices, IBillRepository bills, ILoanRepository loans, IOffBalanceRepository offBalance, INoteTextRepository texts,
            IPaymentApplicationRepository payments)
        {
            _accounts = accounts;
            _lines = lines;
            _profiles = profiles;
            _policies = policies;
            _invoices = invoices;
            _bills = bills;
            _payments = payments;
            _loans = loans;
            _offBalance = offBalance;
            _texts = texts;
        }

        public async Task<NotesDto> Handle(GetNotesQuery request, CancellationToken cancellationToken)
        {
            var from = request.FromDate.Date;
            var to = request.ToDate.Date;
            if (from > to)
                throw new BusinessRuleException("Accounting", "The start date is after the end date.");

            var accounts = await _accounts.GetAllAsync(cancellationToken);
            var profile = await _profiles.GetAsync(cancellationToken);
            var unit = ReportProfileMapper.ResolveUnit(request.Unit, profile);
            var divisor = ReportProfileMapper.Divisor(unit);
            var chart = ChartTemplates.Detect(accounts);
            var prefixes = NotePrefixes.For(chart);

            var before = (await _lines.GetAccountBalancesAsync(null, from.AddDays(-1), null, cancellationToken)).ToDictionary(b => b.AccountId);
            var after = (await _lines.GetAccountBalancesAsync(null, to, null, cancellationToken)).ToDictionary(b => b.AccountId);
            var activity = (await _lines.GetActivityBalancesAsync(from, to, null, cancellationToken)).ToDictionary(b => b.AccountId);
            var result = (await _lines.GetActivityBalancesAsync(from, to, JournalEntry.PeriodClosingType, cancellationToken)).ToDictionary(b => b.AccountId);
            var ledger = new NoteLedger(accounts, before, after, activity, result, divisor);

            var documents = await MaturityData.LoadAsync(_invoices, _bills, _payments, new[] { to }, cancellationToken);
            var loans = await _loans.ListAsync(cancellationToken);
            var maturity = MaturityLedger.For(to, accounts, after, documents.Invoices, documents.Bills, loans, documents.Payments);
            var policy = await _policies.GetAsync(cancellationToken);
            var texts = (await _texts.GetAllAsync(cancellationToken)).ToDictionary(n => n.SectionKey, n => n.Text);

            var notes = new NotesDto
            {
                FromDate = from, ToDate = to, UnitDivisor = divisor,
                Unit = divisor == 1_000m ? "thousand VND" : divisor == 1_000_000m ? "million VND" : "VND",
                Header = ReportProfileMapper.Header(profile),
            };

            NoteSectionDto TextSection(string key)
            {
                var s = NoteTextSections.Find(key)!;
                var text = texts.GetValueOrDefault(key, string.Empty);
                return new NoteSectionDto { Number = s.Number, Title = s.Title, Key = key, Text = text.Length > 0 ? text : null, Hint = text.Length > 0 ? null : s.Hint };
            }

            string D(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            var unitText = unit switch { AmountUnit.Thousand => "nghìn đồng", AmountUnit.Million => "triệu đồng", _ => "đồng" };

            // I. general information: the company block from the profile, then the accountant's text
            var general = TextSection("general");
            var h = notes.Header;
            general.Text = string.Join("\n", new[]
            {
                h?.CompanyName is { Length: > 0 } ? $"Tên doanh nghiệp: {h.CompanyName}" : null,
                h?.TaxCode is { Length: > 0 } ? $"Mã số thuế: {h.TaxCode}" : null,
                h?.Address is { Length: > 0 } ? $"Địa chỉ: {h.Address}" : null,
                general.Text,
            }.Where(x => !string.IsNullOrEmpty(x)));
            if (general.Text.Length == 0)
                general.Text = null;
            else
                general.Hint = null;
            notes.Sections.Add(general);

            // II. accounting period and currency: generated from the closing policy and the unit
            var yearEnd = policy?.FiscalYearEndMonth ?? 12;
            notes.Sections.Add(new NoteSectionDto
            {
                Number = "II", Title = "Kỳ kế toán, đơn vị tiền tệ sử dụng trong kế toán", IsGenerated = true,
                Text = $"Kỳ báo cáo: từ {D(from)} đến {D(to)}.\nNăm tài chính kết thúc vào cuối tháng {yearEnd}.\nĐơn vị tiền tệ sử dụng trong kế toán: đồng Việt Nam (VND).\nĐơn vị tính của báo cáo: {unitText}.",
            });
            notes.Sections.Add(TextSection("standards"));
            foreach (var s in NoteTextSections.All.Where(s => s.Key.StartsWith("policy.")))
                notes.Sections.Add(TextSection(s.Key));

            // V. figures behind the balance sheet
            NoteSectionDto Figures(string number, string title, NoteTableDto table) => new() { Number = number, Title = title, Table = table, IsGenerated = true };
            var any = (string[] p) => p.Length > 0;

            notes.Sections.Add(Figures("V.1", "Tiền và các khoản tương đương tiền", ledger.Movement(new NoteLedger.Group("Cộng", prefixes.Cash, true))));

            var receivables = ledger.Movement(new NoteLedger.Group("Cộng", prefixes.Receivable, true));
            receivables.Rows.Add(new NoteTableRowDto { Label = "Trong đó: đến hạn sau 12 tháng (dài hạn)", Cells = new() { 0m, 0m, 0m, StatementEngine.RoundTo(maturity.Receivable, divisor) } });
            notes.Sections.Add(Figures("V.2", "Phải thu của khách hàng", receivables));

            if (any(prefixes.Inventory))
                notes.Sections.Add(Figures("V.3", "Hàng tồn kho", ledger.Movement(
                    new NoteLedger.Group("Cộng giá gốc hàng tồn kho", prefixes.Inventory, true),
                    new NoteLedger.Group("Dự phòng giảm giá hàng tồn kho", prefixes.InventoryProvision, false))));

            if (any(prefixes.FixedCost))
            {
                var fixedAssets = ledger.Movement(
                    new NoteLedger.Group("Nguyên giá", prefixes.FixedCost, true),
                    new NoteLedger.Group("Giá trị hao mòn lũy kế", prefixes.FixedDepreciation, false));
                var cost = NoteLedger.Subtotal(fixedAssets, "Nguyên giá");
                var depreciation = NoteLedger.Subtotal(fixedAssets, "Giá trị hao mòn lũy kế");
                fixedAssets.Rows.Add(new NoteTableRowDto
                {
                    Label = "Giá trị còn lại", IsTotal = true,
                    Cells = new() { cost[0] - depreciation[0], 0m, 0m, cost[3] - depreciation[3] },
                });
                notes.Sections.Add(Figures("V.4", "Tài sản cố định và bất động sản đầu tư (nguyên giá, hao mòn, giá trị còn lại)", fixedAssets));
            }

            if (any(prefixes.ConstructionInProgress))
                notes.Sections.Add(Figures("V.5", "Chi phí xây dựng cơ bản dở dang", ledger.Movement(new NoteLedger.Group("Cộng", prefixes.ConstructionInProgress, true))));
            if (any(prefixes.Prepaid))
                notes.Sections.Add(Figures("V.6", "Chi phí trả trước", ledger.Movement(new NoteLedger.Group("Cộng", prefixes.Prepaid, true))));

            var payables = ledger.Movement(new NoteLedger.Group("Cộng", prefixes.Payable, false));
            payables.Rows.Add(new NoteTableRowDto { Label = "Trong đó: đến hạn sau 12 tháng (dài hạn)", Cells = new() { 0m, 0m, 0m, StatementEngine.RoundTo(maturity.Payable, divisor) } });
            notes.Sections.Add(Figures("V.7", "Phải trả người bán", payables));
            notes.Sections.Add(Figures("V.8", "Thuế và các khoản phải nộp Nhà nước", ledger.Movement(new NoteLedger.Group("Cộng", prefixes.Tax, false))));
            notes.Sections.Add(Figures("V.9", "Chi phí phải trả và phải trả khác", ledger.Movement(new NoteLedger.Group("Cộng", prefixes.Accrued, false))));
            notes.Sections.Add(Figures("V.10", "Vay và nợ thuê tài chính (từ sổ khoản vay)", BorrowingsTable(loans, accounts, after, to, divisor)));
            notes.Sections.Add(Figures("V.11", "Vốn chủ sở hữu (biến động)", ledger.Movement(new NoteLedger.Group("Cộng", prefixes.Equity, false))));

            // V.12 off-balance items
            var offAccounts = accounts.Where(a => a.AccountType == (int)AccountType.OffBalance).ToDictionary(a => a.Id);
            var posted = (await _offBalance.ListAsync(null, null, to, cancellationToken)).Where(e => e.Status == (int)OffBalanceStatus.Posted).ToList();
            var off = OffBalanceReportBuilder.Build(offAccounts, posted, from, to);
            var offTable = new NoteTableDto { Columns = new() { "Số đầu kỳ", "Tăng", "Giảm", "Số cuối kỳ" } };
            foreach (var l in off.Lines)
                offTable.Rows.Add(new NoteTableRowDto
                {
                    Label = $"{l.AccountNumber} {l.AccountName}",
                    Cells = new() { StatementEngine.RoundTo(l.Opening, divisor), StatementEngine.RoundTo(l.In, divisor), StatementEngine.RoundTo(l.Out, divisor), StatementEngine.RoundTo(l.Closing, divisor) },
                });
            notes.Sections.Add(Figures("V.12", "Các chỉ tiêu ngoài bảng cân đối kế toán", offTable));

            // VI. figures behind the income statement
            notes.Sections.Add(Figures("VI.1", "Doanh thu và thu nhập", ledger.Result("Cộng doanh thu và thu nhập", prefixes.Revenue, debitNormal: false)));
            notes.Sections.Add(Figures("VI.2", "Giá vốn và chi phí", ledger.Result("Cộng giá vốn và chi phí", prefixes.Expenses, debitNormal: true)));

            notes.Sections.Add(TextSection("noncash"));
            notes.Sections.Add(TextSection("subsequent"));
            notes.Sections.Add(TextSection("other"));

            notes.Notes.Add("Draft: the structure follows the Circular 200 form B09-DN; the figures come from the books, the policies and the other texts are written by the accountant (Reports > B09 > Edit texts). The accountant must review the notes before filing.");
            notes.Notes.Add("Amounts are rounded one by one in the chosen unit; a row can differ by a unit from opening + increase − decrease.");
            if (chart == ChartCodes.Simple14)
                notes.Notes.Add("The tenant uses the platform's 14-account chart: only the tables that exist in that chart are filled (no depreciation, provision, prepaid or borrowing accounts).");
            return notes;
        }

        /// <summary>The loan ledger at the end of the range: per loan its principal, what is outstanding, and the parts due within 12 months and later; the ledger balance of 341 as a check.</summary>
        private static NoteTableDto BorrowingsTable(List<Loan> loans, IReadOnlyCollection<Account> accounts, IReadOnlyDictionary<Guid, AccountBalanceSummary> after, DateTime to, decimal divisor)
        {
            decimal R(decimal v) => StatementEngine.RoundTo(v, divisor);
            var table = new NoteTableDto { Columns = new() { "Giá trị gốc", "Dư nợ cuối kỳ", "Đến hạn trong 12 tháng", "Đến hạn sau 12 tháng" } };
            foreach (var loan in loans.Where(l => l.OutstandingAt(to) > 0m).OrderBy(l => l.LoanNumber, StringComparer.Ordinal))
            {
                var outstanding = loan.OutstandingAt(to);
                var later = loan.LongTermAt(to);
                table.Rows.Add(new NoteTableRowDto { Label = $"{loan.LoanNumber} — {loan.Lender}", Cells = new() { R(loan.Principal), R(outstanding), R(outstanding - later), R(later) } });
            }
            table.Rows.Add(new NoteTableRowDto { Label = "Cộng", IsTotal = true, Cells = Enumerable.Range(0, 4).Select(i => table.Rows.Sum(r => r.Cells[i])).ToList() });

            var ledgerBalance = accounts.Where(a => a.AccountNumber.StartsWith("341", StringComparison.Ordinal))
                .Sum(a => after.TryGetValue(a.Id, out var b) ? b.TotalCredit - b.TotalDebit : 0m);
            if (ledgerBalance != 0m || table.Rows.Count > 1)
                table.Rows.Add(new NoteTableRowDto { Label = "Số dư trên sổ cái (TK 341)", Cells = new() { 0m, R(ledgerBalance), 0m, 0m } });
            return table;
        }
    }
}

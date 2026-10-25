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

        /// <summary>"vi" (default) or "en": the fixed wording and the accountant's texts of that language (ROADMAP P1-2j).</summary>
        public string? Language { get; set; }

        /// <summary>The prior period compared with: the tables get a column with its figures. Both dates or none.</summary>
        public DateTime? CompareFromDate { get; set; }
        public DateTime? CompareToDate { get; set; }
    }

    public class NoteTextEntryDto
    {
        public string Key { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Language { get; set; } = NoteStrings.Vietnamese;

        /// <summary>The fiscal year asked for (0 = the texts for every year).</summary>
        public int FiscalYear { get; set; }

        /// <summary>The year the shown text was written for (0 = every year); differs from <see cref="FiscalYear"/> when the text is inherited from an earlier year.</summary>
        public int SourceYear { get; set; }
        public bool Inherited { get; set; }
    }

    public class GetNoteTextsQuery : IRequest<List<NoteTextEntryDto>>
    {
        /// <summary>"vi" (default) or "en".</summary>
        public string? Language { get; set; }

        /// <summary>0 (default) = the texts for every year; a year = that fiscal year (earlier years' texts carry forward).</summary>
        public int FiscalYear { get; set; }
    }

    /// <summary>The fiscal year of a date (the year in which it ends) and the text of a note section for a year: the same year, else the latest earlier one, else year 0.</summary>
    public static class NoteTextVersions
    {
        public static int FiscalYearOf(DateTime date, int yearEndMonth) =>
            yearEndMonth is < 1 or > 12 || yearEndMonth == 12 || date.Month <= yearEndMonth ? date.Year : date.Year + 1;

        /// <summary>Per section key the text to show for a fiscal year and the year it was written for; empty texts do not count (a cleared year falls back).</summary>
        public static Dictionary<string, (string Text, int Year)> Resolve(IEnumerable<NoteText> stored, string language, int fiscalYear) =>
            stored.Where(n => n.Language == language && n.Text.Length > 0 && n.FiscalYear <= fiscalYear)
                .GroupBy(n => n.SectionKey)
                .ToDictionary(g => g.Key, g => { var best = g.OrderByDescending(n => n.FiscalYear).First(); return (best.Text, best.FiscalYear); });
    }

    /// <summary>Saves the texts of the given keys; an empty text clears it. Keys that are not note sections are refused.</summary>
    public class UpdateNoteTextsCommand : IRequest<List<NoteTextEntryDto>>
    {
        /// <summary>"vi" (default) or "en": the language the texts are written in.</summary>
        public string? Language { get; set; }

        /// <summary>0 (default) = the texts for every year; a year = that fiscal year (earlier years' texts carry forward; an empty text clears that year).</summary>
        public int FiscalYear { get; set; }
        public Dictionary<string, string> Texts { get; set; } = new();
    }

    public class GetNoteTextsQueryHandler : IRequestHandler<GetNoteTextsQuery, List<NoteTextEntryDto>>
    {
        private readonly INoteTextRepository _texts;

        public GetNoteTextsQueryHandler(INoteTextRepository texts) => _texts = texts;

        public async Task<List<NoteTextEntryDto>> Handle(GetNoteTextsQuery request, CancellationToken cancellationToken) =>
            NoteTextMapper.Map(await _texts.GetAllAsync(cancellationToken), NoteStrings.Normalize(request.Language), request.FiscalYear);
    }

    internal static class NoteTextMapper
    {
        public static List<NoteTextEntryDto> Map(IEnumerable<NoteText> stored, string language, int fiscalYear)
        {
            var byKey = NoteTextVersions.Resolve(stored, language, fiscalYear);
            return NoteTextSections.All.Select(s =>
            {
                var found = byKey.TryGetValue(s.Key, out var v);
                return new NoteTextEntryDto
                {
                    Key = s.Key, Number = s.Number, Title = s.TitleFor(language), Hint = s.HintFor(language), Text = found ? v.Text : string.Empty, Language = language,
                    FiscalYear = fiscalYear, SourceYear = found ? v.Year : 0, Inherited = found && v.Year != fiscalYear,
                };
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
            var language = NoteStrings.Normalize(request.Language);
            if (request.FiscalYear < 0 || request.FiscalYear > 9999)
                throw new BusinessRuleException("Accounting", "The fiscal year must be 0 (every year) or a year.");
            var unknown = request.Texts.Keys.Where(k => NoteTextSections.Find(k) == null).ToList();
            if (unknown.Count > 0)
                throw new BusinessRuleException("Accounting", $"Unknown note section(s): {string.Join(", ", unknown)}.");

            var stored = await _uow.NoteTexts.GetAllAsync(cancellationToken);
            foreach (var (key, text) in request.Texts)
            {
                var existing = stored.FirstOrDefault(n => n.SectionKey == key && n.Language == language && n.FiscalYear == request.FiscalYear);
                try
                {
                    if (existing != null)
                        existing.Set(text);
                    else if (!string.IsNullOrWhiteSpace(text))
                    {
                        var tenantId = _tenant.CurrentTenantId ?? throw new BusinessRuleException("Accounting", "No tenant in the request.");
                        var created = NoteText.Create(tenantId, key, text, language, request.FiscalYear);
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
            return NoteTextMapper.Map(stored, language, request.FiscalYear);
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
        private readonly IStatementMappingRepository _mappings;

        public GetNotesQueryHandler(
            IAccountRepository accounts, IJournalLineRepository lines, IReportProfileRepository profiles, IClosingPolicyRepository policies,
            IInvoiceRepository invoices, IBillRepository bills, ILoanRepository loans, IOffBalanceRepository offBalance, INoteTextRepository texts,
            IPaymentApplicationRepository payments, IStatementMappingRepository mappings)
        {
            _mappings = mappings;
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
            if (request.CompareFromDate.HasValue != request.CompareToDate.HasValue)
                throw new BusinessRuleException("Accounting", "Give both dates of the comparison period or neither.");
            if (request.CompareFromDate.HasValue && request.CompareFromDate.Value.Date > request.CompareToDate!.Value.Date)
                throw new BusinessRuleException("Accounting", "The comparison start date is after its end date.");

            var language = NoteStrings.Normalize(request.Language);
            var s = NoteStrings.For(language);
            var accounts = await _accounts.GetAllAsync(cancellationToken);
            var profile = await _profiles.GetAsync(cancellationToken);
            var unit = ReportProfileMapper.ResolveUnit(request.Unit, profile);
            var divisor = ReportProfileMapper.Divisor(unit);
            var chart = ChartTemplates.Detect(accounts);
            var prefixes = NoteMappingRows.WithMappings(NotePrefixes.For(chart), await _mappings.GetAllAsync(cancellationToken));

            async Task<NoteLedger> LedgerFor(DateTime f, DateTime t)
            {
                var opening = (await _lines.GetAccountBalancesAsync(null, f.AddDays(-1), null, cancellationToken)).ToDictionary(b => b.AccountId);
                var closing = (await _lines.GetAccountBalancesAsync(null, t, null, cancellationToken)).ToDictionary(b => b.AccountId);
                var activity = (await _lines.GetActivityBalancesAsync(f, t, null, cancellationToken)).ToDictionary(b => b.AccountId);
                var result = (await _lines.GetActivityBalancesAsync(f, t, JournalEntry.PeriodClosingType, cancellationToken)).ToDictionary(b => b.AccountId);
                return new NoteLedger(accounts, opening, closing, activity, result, divisor, s);
            }

            var dates = new List<DateTime> { to };
            if (request.CompareToDate.HasValue)
                dates.Add(request.CompareToDate.Value.Date);
            var documents = await MaturityData.LoadAsync(_invoices, _bills, _payments, dates, cancellationToken);
            var loans = await _loans.ListAsync(cancellationToken);
            var ledger = await LedgerFor(from, to);
            var after = (await _lines.GetAccountBalancesAsync(null, to, null, cancellationToken)).ToDictionary(b => b.AccountId);
            var maturity = MaturityLedger.For(to, accounts, after, documents.Invoices, documents.Bills, loans, documents.Payments);
            var policy = await _policies.GetAsync(cancellationToken);
            var fiscalYear = NoteTextVersions.FiscalYearOf(to, policy?.FiscalYearEndMonth ?? 12);
            var texts = NoteTextVersions.Resolve(await _texts.GetAllAsync(cancellationToken), language, fiscalYear).ToDictionary(p => p.Key, p => p.Value.Text);

            var notes = new NotesDto
            {
                FromDate = from, ToDate = to, UnitDivisor = divisor, Language = language, Title = s.Title, FiscalYear = fiscalYear,
                CompareFromDate = request.CompareFromDate?.Date, CompareToDate = request.CompareToDate?.Date,
                Unit = divisor == 1_000m ? "thousand VND" : divisor == 1_000_000m ? "million VND" : "VND",
                Header = ReportProfileMapper.Header(profile),
            };

            NoteSectionDto TextSection(string key)
            {
                var section = NoteTextSections.Find(key)!;
                var text = texts.GetValueOrDefault(key, string.Empty);
                return new NoteSectionDto { Number = section.Number, Title = section.TitleFor(language), Key = key, Text = text.Length > 0 ? text : null, Hint = text.Length > 0 ? null : section.HintFor(language) };
            }

            string D(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            var unitText = s.UnitText(notes.Unit);

            // I. general information: the company block from the profile, then the accountant's text
            var general = TextSection("general");
            var h = notes.Header;
            general.Text = string.Join("\n", new[]
            {
                h?.CompanyName is { Length: > 0 } ? $"{s.CompanyName}: {h.CompanyName}" : null,
                h?.TaxCode is { Length: > 0 } ? $"{s.TaxCode}: {h.TaxCode}" : null,
                h?.Address is { Length: > 0 } ? $"{s.Address}: {h.Address}" : null,
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
                Number = "II", Title = s.PeriodSectionTitle, IsGenerated = true,
                Text = string.Join("\n", string.Format(s.PeriodLine, D(from), D(to)), string.Format(s.FiscalYearLine, yearEnd), s.CurrencyLine, string.Format(s.UnitLine, unitText)),
            });
            notes.Sections.Add(TextSection("standards"));
            foreach (var section in NoteTextSections.All.Where(x => x.Key.StartsWith("policy.")))
                notes.Sections.Add(TextSection(section.Key));

            // V, VI: the figures; the same tables are built for the prior period when a comparison is asked for, and its column is added by row label
            var figures = Figures(ledger, maturity, prefixes, s, divisor);
            if (request.CompareFromDate.HasValue)
            {
                var priorLedger = await LedgerFor(request.CompareFromDate.Value.Date, request.CompareToDate!.Value.Date);
                var priorAfter = (await _lines.GetAccountBalancesAsync(null, request.CompareToDate.Value.Date, null, cancellationToken)).ToDictionary(b => b.AccountId);
                var priorMaturity = MaturityLedger.For(request.CompareToDate.Value.Date, accounts, priorAfter, documents.Invoices, documents.Bills, loans, documents.Payments);
                var prior = Figures(priorLedger, priorMaturity, prefixes, s, divisor);
                foreach (var (number, (_, table)) in figures)
                    if (prior.TryGetValue(number, out var other))
                        NoteLedger.AddPriorColumn(table, other.Table, number.StartsWith("VI", StringComparison.Ordinal) ? s.PriorAmount : s.PriorClosing);
            }

            NoteSectionDto Figure(string number) => new() { Number = number, Title = figures[number].Title, Table = figures[number].Table, IsGenerated = true };
            NoteSectionDto Extra(string number, string title, NoteTableDto table) => new() { Number = number, Title = title, Table = table, IsGenerated = true };

            foreach (var number in new[] { "V.1", "V.2", "V.3", "V.4", "V.5", "V.6", "V.7", "V.8", "V.9" })
                if (figures.ContainsKey(number))
                    notes.Sections.Add(Figure(number));
            notes.Sections.Add(Extra("V.10", s.Borrowings, BorrowingsTable(loans, accounts, after, to, divisor, s)));
            notes.Sections.Add(Figure("V.11"));

            // V.12 off-balance items
            var offAccounts = accounts.Where(a => a.AccountType == (int)AccountType.OffBalance).ToDictionary(a => a.Id);
            var posted = (await _offBalance.ListAsync(null, null, to, cancellationToken)).Where(e => e.Status == (int)OffBalanceStatus.Posted).ToList();
            var off = OffBalanceReportBuilder.Build(offAccounts, posted, from, to);
            var offTable = new NoteTableDto { Columns = new() { s.Opening, s.Increase, s.Decrease, s.Closing } };
            foreach (var l in off.Lines)
                offTable.Rows.Add(new NoteTableRowDto
                {
                    Label = $"{l.AccountNumber} {l.AccountName}",
                    Cells = new() { StatementEngine.RoundTo(l.Opening, divisor), StatementEngine.RoundTo(l.In, divisor), StatementEngine.RoundTo(l.Out, divisor), StatementEngine.RoundTo(l.Closing, divisor) },
                });
            notes.Sections.Add(Extra("V.12", s.OffBalance, offTable));

            // VI. figures behind the income statement
            notes.Sections.Add(Figure("VI.1"));
            notes.Sections.Add(Figure("VI.2"));

            // VII. non-cash transactions: found in the journal, then the accountant's text
            var noncash = TextSection("noncash");
            var journal = await _lines.GetLedgerLinesAsync(accounts.Select(a => a.Id).ToList(), from, to, cancellationToken) ?? new List<LedgerRow>();
            var nonCashTable = NonCashTransactions.Build(accounts, journal, prefixes, s, divisor);
            if (nonCashTable.Rows.Count > 0)
            {
                noncash.Table = nonCashTable;
                noncash.Hint = noncash.Text == null ? noncash.Hint : null;
            }
            notes.Sections.Add(noncash);
            notes.Sections.Add(TextSection("subsequent"));
            notes.Sections.Add(TextSection("other"));

            notes.Notes.Add("Draft: the structure follows the Circular 200 form B09-DN; the figures come from the books, the policies and the other texts are written by the accountant (Reports > B09 > Edit texts). The accountant must review the notes before filing.");
            notes.Notes.Add("Amounts are rounded one by one in the chosen unit; a row can differ by a unit from opening + increase − decrease.");
            if (chart == ChartCodes.Simple14)
                notes.Notes.Add("The tenant uses the platform's 14-account chart: only the tables that exist in that chart are filled (no depreciation, provision, prepaid or borrowing accounts).");
            if (maturity.Estimated)
                notes.Notes.Add("Some payments used for the long-term split of trade receivables and payables have no recorded date, so that split is approximate.");
            if (nonCashTable.Rows.Count > 0)
                notes.Notes.Add("The non-cash table is derived from journal entries by their accounts only (fixed assets bought on a liability, a liability moved into equity); confirm it in the text of section VII.");
            return notes;
        }

        /// <summary>The tables behind the balance sheet and the income statement for one range, by section number (V.1 … V.9, V.11, VI.1, VI.2), in the language of <paramref name="s"/>.</summary>
        private static Dictionary<string, (string Title, NoteTableDto Table)> Figures(NoteLedger ledger, StatementMaturity maturity, NotePrefixes prefixes, NoteStrings s, decimal divisor)
        {
            var result = new Dictionary<string, (string, NoteTableDto)>();
            NoteTableDto Rows(params NoteLedger.Group[] groups) => ledger.Movement(groups);
            NoteTableRowDto LongTerm(decimal value) => new() { Label = s.OfWhichLongTerm, Cells = new() { 0m, 0m, 0m, StatementEngine.RoundTo(value, divisor) } };

            result["V.1"] = (s.Cash, Rows(new NoteLedger.Group(s.Total, prefixes.Cash, true)));

            var receivables = Rows(new NoteLedger.Group(s.Total, prefixes.Receivable, true));
            receivables.Rows.Add(LongTerm(maturity.Receivable));
            result["V.2"] = (s.Receivables, receivables);

            if (prefixes.Inventory.Length > 0)
                result["V.3"] = (s.Inventories, Rows(
                    new NoteLedger.Group(s.InventoryCostTotal, prefixes.Inventory, true),
                    new NoteLedger.Group(s.InventoryProvision, prefixes.InventoryProvision, false)));

            if (prefixes.FixedCost.Length > 0)
            {
                var fixedAssets = Rows(new NoteLedger.Group(s.Cost, prefixes.FixedCost, true), new NoteLedger.Group(s.AccumulatedDepreciation, prefixes.FixedDepreciation, false));
                var cost = NoteLedger.Subtotal(fixedAssets, s.Cost);
                var depreciation = NoteLedger.Subtotal(fixedAssets, s.AccumulatedDepreciation);
                fixedAssets.Rows.Add(new NoteTableRowDto { Label = s.NetBookValue, IsTotal = true, Cells = new() { cost[0] - depreciation[0], 0m, 0m, cost[3] - depreciation[3] } });
                result["V.4"] = (s.FixedAssets, fixedAssets);
            }

            if (prefixes.ConstructionInProgress.Length > 0)
                result["V.5"] = (s.ConstructionInProgress, Rows(new NoteLedger.Group(s.Total, prefixes.ConstructionInProgress, true)));
            if (prefixes.Prepaid.Length > 0)
                result["V.6"] = (s.Prepaid, Rows(new NoteLedger.Group(s.Total, prefixes.Prepaid, true)));

            var payables = Rows(new NoteLedger.Group(s.Total, prefixes.Payable, false));
            payables.Rows.Add(LongTerm(maturity.Payable));
            result["V.7"] = (s.Payables, payables);
            result["V.8"] = (s.Taxes, Rows(new NoteLedger.Group(s.Total, prefixes.Tax, false)));
            result["V.9"] = (s.Accrued, Rows(new NoteLedger.Group(s.Total, prefixes.Accrued, false)));
            result["V.11"] = (s.Equity, Rows(new NoteLedger.Group(s.Total, prefixes.Equity, false)));
            result["VI.1"] = (s.Revenue, ledger.Result(s.RevenueTotal, prefixes.Revenue, debitNormal: false));
            result["VI.2"] = (s.Expenses, ledger.Result(s.ExpensesTotal, prefixes.Expenses, debitNormal: true));
            return result;
        }

        /// <summary>The loan ledger at the end of the range: per loan its principal, what is outstanding, and the parts due within 12 months and later; the ledger balance of 341 as a check.</summary>
        private static NoteTableDto BorrowingsTable(List<Loan> loans, IReadOnlyCollection<Account> accounts, IReadOnlyDictionary<Guid, AccountBalanceSummary> after, DateTime to, decimal divisor, NoteStrings s)
        {
            decimal R(decimal v) => StatementEngine.RoundTo(v, divisor);
            var table = new NoteTableDto { Columns = new() { s.LoanPrincipal, s.LoanOutstanding, s.LoanWithin12, s.LoanAfter12 } };
            foreach (var loan in loans.Where(l => l.OutstandingAt(to) > 0m).OrderBy(l => l.LoanNumber, StringComparer.Ordinal))
            {
                var outstanding = loan.OutstandingAt(to);
                var later = loan.LongTermAt(to);
                table.Rows.Add(new NoteTableRowDto { Label = $"{loan.LoanNumber} — {loan.Lender}", Cells = new() { R(loan.Principal), R(outstanding), R(outstanding - later), R(later) } });
            }
            table.Rows.Add(new NoteTableRowDto { Label = s.Total, IsTotal = true, Cells = Enumerable.Range(0, 4).Select(i => table.Rows.Sum(r => r.Cells[i])).ToList() });

            var ledgerBalance = accounts.Where(a => a.AccountNumber.StartsWith("341", StringComparison.Ordinal))
                .Sum(a => after.TryGetValue(a.Id, out var b) ? b.TotalCredit - b.TotalDebit : 0m);
            if (ledgerBalance != 0m || table.Rows.Count > 1)
                table.Rows.Add(new NoteTableRowDto { Label = s.LedgerBalance341, Cells = new() { 0m, R(ledgerBalance), 0m, 0m } });
            return table;
        }
    }
}

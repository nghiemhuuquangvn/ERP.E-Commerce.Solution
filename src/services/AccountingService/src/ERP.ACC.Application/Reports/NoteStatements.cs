using ERP.ACC.Application.DTOs;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;

namespace ERP.ACC.Application.Reports
{
    // ---- the notes to the financial statements B09-DN (ROADMAP P1-2i) -----------------------------------------------------------------------------------

    public class NoteTableRowDto
    {
        public string Label { get; set; } = string.Empty;
        public List<decimal> Cells { get; set; } = new();

        /// <summary>A subtotal or total row (shown in bold).</summary>
        public bool IsTotal { get; set; }
    }

    public class NoteTableDto
    {
        public List<string> Columns { get; set; } = new();
        public List<NoteTableRowDto> Rows { get; set; } = new();
    }

    public class NoteSectionDto
    {
        /// <summary>"I", "IV.2", "V.4" …</summary>
        public string Number { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        /// <summary>The key of the text the accountant writes for this section; null when the section has no text of its own.</summary>
        public string? Key { get; set; }
        public string? Text { get; set; }

        /// <summary>What to write here (shown while the text is empty).</summary>
        public string? Hint { get; set; }

        /// <summary>The text was generated from the company profile / settings, not written by the accountant.</summary>
        public bool IsGenerated { get; set; }
        public NoteTableDto? Table { get; set; }
    }

    public class NotesDto
    {
        public string LayoutId { get; set; } = "B09-DN";

        /// <summary>"vi", "en", "ja", "zh" or "fr": the language of the fixed wording (see <see cref="NoteStrings"/>).</summary>
        public string Language { get; set; } = NoteStrings.Vietnamese;

        /// <summary>The fiscal year (the year in which it ends) whose texts the notes show.</summary>
        public int FiscalYear { get; set; }
        public string Title { get; set; } = "Bản thuyết minh báo cáo tài chính (Mẫu B09-DN) — bản nháp";
        public bool IsDraft { get; set; } = true;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        /// <summary>The prior period compared with (the tables get a column with its figures); null when none was asked for.</summary>
        public DateTime? CompareFromDate { get; set; }
        public DateTime? CompareToDate { get; set; }
        public string Unit { get; set; } = "VND";
        public decimal UnitDivisor { get; set; } = 1m;
        public ReportHeaderDto? Header { get; set; }
        public List<NoteSectionDto> Sections { get; set; } = new();
        public List<string> Notes { get; set; } = new();
    }

    /// <summary>The sections of the notes whose text the accountant writes: key, number, title (Vietnamese form) and a hint.</summary>
    public static class NoteTextSections
    {
        public sealed record Section(string Key, string Number, string Title, string Hint, string TitleEn, string HintEn)
        {
            public string TitleFor(string language) =>
                language == NoteStrings.Vietnamese ? Title : language == NoteStrings.English ? TitleEn : NoteSectionTranslations.Find(Key, language)?.Title ?? TitleEn;

            public string HintFor(string language) =>
                language == NoteStrings.Vietnamese ? Hint : language == NoteStrings.English ? HintEn : NoteSectionTranslations.Find(Key, language)?.Hint ?? HintEn;
        }

        public static IReadOnlyList<Section> All { get; } = new[]
        {
            new Section("general", "I", "Đặc điểm hoạt động của doanh nghiệp", "Hình thức sở hữu vốn, lĩnh vực kinh doanh, ngành nghề, chu kỳ sản xuất kinh doanh, cấu trúc doanh nghiệp.", "Nature of operations", "Form of ownership, line of business, business cycle, group structure."),
            new Section("standards", "III", "Chuẩn mực và Chế độ kế toán áp dụng", "Chế độ kế toán áp dụng (Thông tư 99/2025/TT-BTC hoặc Thông tư 200/2014/TT-BTC), tuyên bố tuân thủ chuẩn mực.", "Accounting standards and regime applied", "The regime applied (Circular 99/2025/TT-BTC or 200/2014/TT-BTC) and the statement of compliance with the standards."),
            new Section("policy.cash", "IV.1", "Chính sách kế toán: tiền và các khoản tương đương tiền", "Nguyên tắc ghi nhận tiền và tương đương tiền; quy đổi ngoại tệ.", "Accounting policy: cash and cash equivalents", "Recognition of cash and cash equivalents; foreign currency translation."),
            new Section("policy.receivables", "IV.2", "Chính sách kế toán: các khoản phải thu", "Nguyên tắc ghi nhận, phân loại ngắn hạn / dài hạn, lập dự phòng phải thu khó đòi.", "Accounting policy: receivables", "Recognition, short- and long-term classification, provision for doubtful debts."),
            new Section("policy.inventory", "IV.3", "Chính sách kế toán: hàng tồn kho", "Nguyên tắc đánh giá hàng tồn kho, phương pháp tính giá xuất kho (bình quân, FIFO), dự phòng giảm giá.", "Accounting policy: inventories", "Valuation of inventories, cost formula (average, FIFO), write-down provision."),
            new Section("policy.fixedassets", "IV.4", "Chính sách kế toán: tài sản cố định và khấu hao", "Nguyên tắc ghi nhận nguyên giá, phương pháp khấu hao, thời gian hữu dụng.", "Accounting policy: fixed assets and depreciation", "Recognition of cost, depreciation method, useful lives."),
            new Section("policy.provisions", "IV.5", "Chính sách kế toán: các khoản dự phòng", "Nguyên tắc ghi nhận các khoản dự phòng phải trả.", "Accounting policy: provisions", "Recognition of provisions for liabilities."),
            new Section("policy.revenue", "IV.6", "Chính sách kế toán: ghi nhận doanh thu và chi phí", "Điều kiện ghi nhận doanh thu bán hàng, cung cấp dịch vụ, doanh thu tài chính; ghi nhận chi phí.", "Accounting policy: revenue and expense recognition", "Conditions for recognising revenue from goods, services and finance income; recognition of expenses."),
            new Section("policy.tax", "IV.7", "Chính sách kế toán: thuế", "Thuế suất thuế TNDN, thuế GTGT và các loại thuế khác; thuế thu nhập hoãn lại.", "Accounting policy: taxes", "Corporate income tax rate, VAT and other taxes; deferred tax."),
            new Section("policy.fx", "IV.8", "Chính sách kế toán: ngoại tệ", "Tỷ giá quy đổi và xử lý chênh lệch tỷ giá.", "Accounting policy: foreign currency", "Exchange rates used and treatment of exchange differences."),
            new Section("noncash", "VII", "Thông tin bổ sung cho Báo cáo lưu chuyển tiền tệ", "Các giao dịch không bằng tiền ảnh hưởng đến báo cáo lưu chuyển tiền tệ (mua tài sản bằng nợ, chuyển nợ thành vốn …).", "Supplementary information to the cash flow statement", "Non-cash transactions that affect the cash flow statement (assets bought on debt, debt converted into equity …)."),
            new Section("subsequent", "VIII.1", "Những sự kiện phát sinh sau ngày kết thúc kỳ kế toán", "Các sự kiện quan trọng xảy ra sau ngày lập báo cáo.", "Events after the end of the accounting period", "Significant events after the reporting date."),
            new Section("other", "VIII.2", "Những thông tin khác", "Các thông tin khác cần thuyết minh (bên liên quan, nợ tiềm tàng, cam kết …).", "Other information", "Other disclosures (related parties, contingent liabilities, commitments …)."),
        };

        public static Section? Find(string key) => All.FirstOrDefault(s => s.Key == key);
    }

    /// <summary>The account prefixes behind each table of the notes, per chart of accounts.</summary>
    public sealed record NotePrefixes(
        string[] Cash, string[] Receivable, string[] Inventory, string[] InventoryProvision, string[] FixedCost, string[] FixedDepreciation,
        string[] ConstructionInProgress, string[] Prepaid, string[] Payable, string[] Tax, string[] Accrued, string[] Equity, string[] Revenue, string[] Expenses)
    {
        public static readonly NotePrefixes Vas99 = new(
            new[] { "111", "112", "113" }, new[] { "131" }, new[] { "151", "152", "153", "154", "155", "156", "157", "158" }, new[] { "2294" },
            new[] { "211", "212", "213", "217" }, new[] { "214" }, new[] { "241" }, new[] { "142", "242" }, new[] { "331" }, new[] { "333" },
            new[] { "335", "338" }, new[] { "411", "412", "413", "414", "418", "419", "421" },
            new[] { "511", "512", "515", "711" }, new[] { "521", "632", "635", "641", "642", "811", "821" });

        public static readonly NotePrefixes Simple14 = new(
            new[] { "1000" }, new[] { "1100" }, new[] { "1200" }, Array.Empty<string>(), new[] { "1500" }, Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>(), new[] { "2000" }, new[] { "2200" }, new[] { "2100" }, new[] { "3000", "3100" },
            new[] { "4000", "4100" }, new[] { "5000", "5100", "5200" });

        public static NotePrefixes For(string chartTemplate) => chartTemplate == ChartCodes.Simple14 ? Simple14 : Vas99;
    }

    internal static class ChartCodes
    {
        public const string Simple14 = "Simple14";
    }

    /// <summary>
    /// Builds the figures of the notes from the ledger: for a group of accounts, per account, the opening balance, the increase, the decrease and the closing
    /// balance on the group's natural side (debit for assets, credit for liabilities and equity). Amounts are rounded in the report's unit row by row; a subtotal
    /// adds the rounded rows, so a column foots (the closing column may differ by a unit from opening + increase − decrease: the amounts are rounded one by one).
    /// </summary>
    public sealed class NoteLedger
    {
        private readonly IReadOnlyCollection<Account> _accounts;
        private readonly IReadOnlyDictionary<Guid, AccountBalanceSummary> _before;
        private readonly IReadOnlyDictionary<Guid, AccountBalanceSummary> _after;
        private readonly IReadOnlyDictionary<Guid, AccountBalanceSummary> _activity;
        private readonly IReadOnlyDictionary<Guid, AccountBalanceSummary> _periodResult;
        private readonly decimal _divisor;
        private readonly NoteStrings _strings;

        /// <param name="before">Cumulative balances the day before the range.</param>
        /// <param name="after">Cumulative balances at the end of the range.</param>
        /// <param name="activity">Activity of the range with every entry (closing entries included): the movement of the balance-sheet accounts.</param>
        /// <param name="periodResult">Activity of the range without the closing entries: the revenue and expenses of the income statement.</param>
        public NoteLedger(
            IReadOnlyCollection<Account> accounts,
            IReadOnlyDictionary<Guid, AccountBalanceSummary> before, IReadOnlyDictionary<Guid, AccountBalanceSummary> after,
            IReadOnlyDictionary<Guid, AccountBalanceSummary> activity, IReadOnlyDictionary<Guid, AccountBalanceSummary> periodResult, decimal divisor, NoteStrings? strings = null)
        {
            _strings = strings ?? NoteStrings.Vi;
            _accounts = accounts;
            _before = before;
            _after = after;
            _activity = activity;
            _periodResult = periodResult;
            _divisor = divisor;
        }

        public sealed record Group(string Label, string[] Prefixes, bool DebitNormal);

        public static readonly string[] MovementColumns = { "Số đầu kỳ", "Tăng", "Giảm", "Số cuối kỳ" };

        private string[] Columns => new[] { _strings.Opening, _strings.Increase, _strings.Decrease, _strings.Closing };

        private decimal R(decimal value) => StatementEngine.RoundTo(value, _divisor);

        private static decimal Signed(IReadOnlyDictionary<Guid, AccountBalanceSummary> balances, Account a, bool debitNormal) =>
            balances.TryGetValue(a.Id, out var b) ? (debitNormal ? b.TotalDebit - b.TotalCredit : b.TotalCredit - b.TotalDebit) : 0m;

        private IEnumerable<Account> Matching(Group g) => _accounts
            .Where(a => g.Prefixes.Any(p => a.AccountNumber.StartsWith(p, StringComparison.Ordinal)))
            .OrderBy(a => a.AccountNumber, StringComparer.Ordinal);

        /// <summary>Opening / increase / decrease / closing per account of each group, with a subtotal per group; the groups are listed one after the other.</summary>
        public NoteTableDto Movement(params Group[] groups)
        {
            var table = new NoteTableDto { Columns = Columns.ToList() };
            foreach (var group in groups)
            {
                var rows = new List<NoteTableRowDto>();
                foreach (var account in Matching(group))
                {
                    var opening = Signed(_before, account, group.DebitNormal);
                    var closing = Signed(_after, account, group.DebitNormal);
                    var inc = _activity.TryGetValue(account.Id, out var act) ? (group.DebitNormal ? act.TotalDebit : act.TotalCredit) : 0m;
                    var dec = act != null ? (group.DebitNormal ? act.TotalCredit : act.TotalDebit) : 0m;
                    if (opening == 0m && inc == 0m && dec == 0m && closing == 0m)
                        continue;
                    rows.Add(new NoteTableRowDto { Label = $"{account.AccountNumber} {account.Name}", Cells = new() { R(opening), R(inc), R(dec), R(closing) } });
                }

                table.Rows.AddRange(rows);
                if (rows.Count > 0 || groups.Length > 1)
                    table.Rows.Add(new NoteTableRowDto
                    {
                        Label = group.Label, IsTotal = true,
                        Cells = Enumerable.Range(0, 4).Select(i => rows.Sum(r => r.Cells[i])).ToList(),
                    });
            }
            return table;
        }

        /// <summary>The group's subtotal row of a table built by <see cref="Movement"/> (by label), or zeros.</summary>
        public static List<decimal> Subtotal(NoteTableDto table, string label) =>
            table.Rows.FirstOrDefault(r => r.IsTotal && r.Label == label)?.Cells.ToList() ?? new List<decimal> { 0m, 0m, 0m, 0m };

        /// <summary>
        /// Adds the prior period to a table: one more column with the prior closing balance (a movement table) or the prior amount (a result table), found by row label.
        /// A row the prior period does not have shows 0.
        /// </summary>
        public static void AddPriorColumn(NoteTableDto table, NoteTableDto prior, string columnLabel)
        {
            var source = prior.Columns.Count - 1;
            table.Columns.Add(columnLabel);
            foreach (var row in table.Rows)
            {
                var match = prior.Rows.FirstOrDefault(r => r.Label == row.Label && r.IsTotal == row.IsTotal);
                var width = table.Columns.Count - 1;
                while (row.Cells.Count < width)
                    row.Cells.Add(0m);
                row.Cells.Add(match != null && match.Cells.Count > source ? match.Cells[source] : 0m);
            }
        }

        /// <summary>One column per account group: the revenue or expense of the range (closing entries left out), per account, with a total.</summary>
        public NoteTableDto Result(string totalLabel, string[] prefixes, bool debitNormal)
        {
            var table = new NoteTableDto { Columns = new() { _strings.Amount } };
            foreach (var account in Matching(new Group(totalLabel, prefixes, debitNormal)))
            {
                var value = Signed(_periodResult, account, debitNormal);
                if (value == 0m)
                    continue;
                table.Rows.Add(new NoteTableRowDto { Label = $"{account.AccountNumber} {account.Name}", Cells = new() { R(value) } });
            }
            table.Rows.Add(new NoteTableRowDto { Label = totalLabel, IsTotal = true, Cells = new() { table.Rows.Sum(r => r.Cells[0]) } });
            return table;
        }
    }
}

using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.ACC.Application.Reports
{
    /// <summary>
    /// Every fixed wording of the notes B09-DN in one language (ROADMAP P1-2j): section and table titles, column and row labels, the lines of the generated text and the
    /// labels of the PDF. Vietnamese is the form the Circular prescribes; English is for tenants that file or share the notes in English. The accountant's own texts are
    /// stored per language (<c>NoteText.Language</c>). The report header comes from the report profile and is not translated.
    /// </summary>
    public sealed class NoteStrings
    {
        public const string Vietnamese = "vi";
        public const string English = "en";

        public string Language { get; init; } = Vietnamese;
        public string Title { get; init; } = string.Empty;
        public string Draft { get; init; } = string.Empty;
        public string CompanyName { get; init; } = string.Empty;
        public string TaxCode { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string PeriodSectionTitle { get; init; } = string.Empty;
        public string PeriodLine { get; init; } = string.Empty;       // {0} from, {1} to
        public string FiscalYearLine { get; init; } = string.Empty;   // {0} month
        public string CurrencyLine { get; init; } = string.Empty;
        public string UnitLine { get; init; } = string.Empty;         // {0} unit text
        public string UnitDong { get; init; } = string.Empty;
        public string UnitThousand { get; init; } = string.Empty;
        public string UnitMillion { get; init; } = string.Empty;

        // table titles
        public string Cash { get; init; } = string.Empty;
        public string Receivables { get; init; } = string.Empty;
        public string Inventories { get; init; } = string.Empty;
        public string FixedAssets { get; init; } = string.Empty;
        public string ConstructionInProgress { get; init; } = string.Empty;
        public string Prepaid { get; init; } = string.Empty;
        public string Payables { get; init; } = string.Empty;
        public string Taxes { get; init; } = string.Empty;
        public string Accrued { get; init; } = string.Empty;
        public string Borrowings { get; init; } = string.Empty;
        public string Equity { get; init; } = string.Empty;
        public string OffBalance { get; init; } = string.Empty;
        public string Revenue { get; init; } = string.Empty;
        public string Expenses { get; init; } = string.Empty;
        public string NonCashTitle { get; init; } = string.Empty;

        // row / column labels
        public string Total { get; init; } = string.Empty;
        public string OfWhichLongTerm { get; init; } = string.Empty;
        public string InventoryCostTotal { get; init; } = string.Empty;
        public string InventoryProvision { get; init; } = string.Empty;
        public string Cost { get; init; } = string.Empty;
        public string AccumulatedDepreciation { get; init; } = string.Empty;
        public string NetBookValue { get; init; } = string.Empty;
        public string RevenueTotal { get; init; } = string.Empty;
        public string ExpensesTotal { get; init; } = string.Empty;
        public string LedgerBalance341 { get; init; } = string.Empty;
        public string NonCashAssetsOnCredit { get; init; } = string.Empty;
        public string NonCashDebtToEquity { get; init; } = string.Empty;
        public string Opening { get; init; } = string.Empty;
        public string Increase { get; init; } = string.Empty;
        public string Decrease { get; init; } = string.Empty;
        public string Closing { get; init; } = string.Empty;
        public string Amount { get; init; } = string.Empty;
        public string PriorClosing { get; init; } = string.Empty;
        public string PriorAmount { get; init; } = string.Empty;
        public string LoanPrincipal { get; init; } = string.Empty;
        public string LoanOutstanding { get; init; } = string.Empty;
        public string LoanWithin12 { get; init; } = string.Empty;
        public string LoanAfter12 { get; init; } = string.Empty;

        // the PDF
        public string PdfPage { get; init; } = string.Empty;
        public string PdfTaxCode { get; init; } = string.Empty;
        public string PdfAddress { get; init; } = string.Empty;
        public string PdfDraft { get; init; } = string.Empty;
        public string PdfPeriod { get; init; } = string.Empty;        // {0} from, {1} to, {2} unit
        public string PdfNotWritten { get; init; } = string.Empty;    // {0} hint
        public string PdfNoData { get; init; } = string.Empty;
        public string PdfNotePrefix { get; init; } = string.Empty;
        public string PdfItem { get; init; } = string.Empty;
        public string PdfPreparedBy { get; init; } = string.Empty;
        public string PdfChiefAccountant { get; init; } = string.Empty;
        public string PdfDirector { get; init; } = string.Empty;

        public string UnitText(string unit) => unit.StartsWith("thousand", StringComparison.Ordinal) ? UnitThousand : unit.StartsWith("million", StringComparison.Ordinal) ? UnitMillion : UnitDong;

        /// <summary>"vi" (default, also for an empty value) or "en"; anything else is refused.</summary>
        public static string Normalize(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return Vietnamese;
            var code = language.Trim().ToLowerInvariant();
            if (code.StartsWith("vi", StringComparison.Ordinal))
                return Vietnamese;
            if (code.StartsWith("en", StringComparison.Ordinal))
                return English;
            throw new BusinessRuleException("Accounting", $"Unsupported language '{language}'. Use vi or en.");
        }

        public static NoteStrings For(string? language) => Normalize(language) == English ? En : Vi;

        public static readonly NoteStrings Vi = new()
        {
            Language = Vietnamese,
            Title = "Bản thuyết minh báo cáo tài chính (Mẫu B09-DN) — bản nháp",
            Draft = "Bản nháp — kế toán cần rà soát trước khi nộp",
            CompanyName = "Tên doanh nghiệp", TaxCode = "Mã số thuế", Address = "Địa chỉ",
            PeriodSectionTitle = "Kỳ kế toán, đơn vị tiền tệ sử dụng trong kế toán",
            PeriodLine = "Kỳ báo cáo: từ {0} đến {1}.", FiscalYearLine = "Năm tài chính kết thúc vào cuối tháng {0}.",
            CurrencyLine = "Đơn vị tiền tệ sử dụng trong kế toán: đồng Việt Nam (VND).", UnitLine = "Đơn vị tính của báo cáo: {0}.",
            UnitDong = "đồng", UnitThousand = "nghìn đồng", UnitMillion = "triệu đồng",
            Cash = "Tiền và các khoản tương đương tiền", Receivables = "Phải thu của khách hàng", Inventories = "Hàng tồn kho",
            FixedAssets = "Tài sản cố định và bất động sản đầu tư (nguyên giá, hao mòn, giá trị còn lại)", ConstructionInProgress = "Chi phí xây dựng cơ bản dở dang",
            Prepaid = "Chi phí trả trước", Payables = "Phải trả người bán", Taxes = "Thuế và các khoản phải nộp Nhà nước", Accrued = "Chi phí phải trả và phải trả khác",
            Borrowings = "Vay và nợ thuê tài chính (từ sổ khoản vay)", Equity = "Vốn chủ sở hữu (biến động)", OffBalance = "Các chỉ tiêu ngoài bảng cân đối kế toán",
            Revenue = "Doanh thu và thu nhập", Expenses = "Giá vốn và chi phí", NonCashTitle = "Các giao dịch không bằng tiền (tính từ sổ nhật ký)",
            Total = "Cộng", OfWhichLongTerm = "Trong đó: đến hạn sau 12 tháng (dài hạn)", InventoryCostTotal = "Cộng giá gốc hàng tồn kho", InventoryProvision = "Dự phòng giảm giá hàng tồn kho",
            Cost = "Nguyên giá", AccumulatedDepreciation = "Giá trị hao mòn lũy kế", NetBookValue = "Giá trị còn lại",
            RevenueTotal = "Cộng doanh thu và thu nhập", ExpensesTotal = "Cộng giá vốn và chi phí", LedgerBalance341 = "Số dư trên sổ cái (TK 341)",
            NonCashAssetsOnCredit = "Mua tài sản cố định bằng nợ phải trả", NonCashDebtToEquity = "Chuyển nợ phải trả thành vốn chủ sở hữu",
            Opening = "Số đầu kỳ", Increase = "Tăng", Decrease = "Giảm", Closing = "Số cuối kỳ", Amount = "Số tiền", PriorClosing = "Số cuối kỳ trước", PriorAmount = "Kỳ trước",
            LoanPrincipal = "Giá trị gốc", LoanOutstanding = "Dư nợ cuối kỳ", LoanWithin12 = "Đến hạn trong 12 tháng", LoanAfter12 = "Đến hạn sau 12 tháng",
            PdfPage = "Trang ", PdfTaxCode = "Mã số thuế: ", PdfAddress = "Địa chỉ: ", PdfDraft = "Bản nháp — kế toán cần rà soát trước khi nộp",
            PdfPeriod = "Từ {0} đến {1} — Đơn vị tính: {2}", PdfNotWritten = "(Chưa có nội dung — {0})", PdfNoData = "(Không phát sinh số liệu trong kỳ)", PdfNotePrefix = "Ghi chú: ",
            PdfItem = "Chỉ tiêu", PdfPreparedBy = "Người lập biểu", PdfChiefAccountant = "Kế toán trưởng", PdfDirector = "Giám đốc",
        };

        public static readonly NoteStrings En = new()
        {
            Language = English,
            Title = "Notes to the financial statements (Form B09-DN) — draft",
            Draft = "Draft — the accountant must review before filing",
            CompanyName = "Company name", TaxCode = "Tax code", Address = "Address",
            PeriodSectionTitle = "Accounting period and currency used in accounting",
            PeriodLine = "Reporting period: from {0} to {1}.", FiscalYearLine = "The financial year ends at the end of month {0}.",
            CurrencyLine = "Currency used in accounting: Vietnamese dong (VND).", UnitLine = "Unit of the report: {0}.",
            UnitDong = "dong", UnitThousand = "thousand dong", UnitMillion = "million dong",
            Cash = "Cash and cash equivalents", Receivables = "Trade receivables", Inventories = "Inventories",
            FixedAssets = "Fixed assets and investment property (cost, depreciation, net book value)", ConstructionInProgress = "Construction in progress",
            Prepaid = "Prepaid expenses", Payables = "Trade payables", Taxes = "Taxes and amounts payable to the State", Accrued = "Accrued expenses and other payables",
            Borrowings = "Borrowings and finance lease liabilities (from the loan ledger)", Equity = "Owners' equity (movement)", OffBalance = "Off-balance-sheet items",
            Revenue = "Revenue and income", Expenses = "Cost of sales and expenses", NonCashTitle = "Non-cash transactions (from the journal)",
            Total = "Total", OfWhichLongTerm = "Of which: due after 12 months (long-term)", InventoryCostTotal = "Total inventory cost", InventoryProvision = "Provision for inventory write-down",
            Cost = "Cost", AccumulatedDepreciation = "Accumulated depreciation", NetBookValue = "Net book value",
            RevenueTotal = "Total revenue and income", ExpensesTotal = "Total cost of sales and expenses", LedgerBalance341 = "Ledger balance (account 341)",
            NonCashAssetsOnCredit = "Fixed assets acquired on credit", NonCashDebtToEquity = "Debt converted into equity",
            Opening = "Opening", Increase = "Increase", Decrease = "Decrease", Closing = "Closing", Amount = "Amount", PriorClosing = "Prior closing", PriorAmount = "Prior period",
            LoanPrincipal = "Principal", LoanOutstanding = "Outstanding at period end", LoanWithin12 = "Due within 12 months", LoanAfter12 = "Due after 12 months",
            PdfPage = "Page ", PdfTaxCode = "Tax code: ", PdfAddress = "Address: ", PdfDraft = "Draft — the accountant must review before filing",
            PdfPeriod = "From {0} to {1} — Unit: {2}", PdfNotWritten = "(Not written yet — {0})", PdfNoData = "(No activity in the period)", PdfNotePrefix = "Note: ",
            PdfItem = "Item", PdfPreparedBy = "Prepared by", PdfChiefAccountant = "Chief accountant", PdfDirector = "Director",
        };
    }
}

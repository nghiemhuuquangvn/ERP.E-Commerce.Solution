using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;

namespace ERP.ACC.Application.Posting
{
    /// <summary>
    /// A ready-made chart of accounts plus the account each posting key uses in it (ROADMAP P1-2). A tenant gets one through lazy provisioning
    /// (<c>Accounting:DefaultChartTemplate</c>) or <c>POST accounting/chart/apply-template</c>; afterwards the chart and the posting profile are
    /// the tenant's own and can be edited freely.
    /// </summary>
    public sealed record ChartTemplate(
        string Id,
        string Name,
        string Description,
        IReadOnlyList<ChartTemplate.Entry> Accounts,
        IReadOnlyDictionary<string, string> PostingMap)
    {
        /// <param name="ParentNumber">null = top level; the parent must be listed before its children.</param>
        public sealed record Entry(
            string Number, string Name, AccountType Type, NormalBalance Normal, string? ParentNumber = null, bool IsControl = false,
            bool IsCash = false, CashFlowCategory? CashFlow = null);

        /// <summary>Builds the accounts of the template, parents first, with parent links and levels set.</summary>
        public List<Account> BuildAccounts()
        {
            var byNumber = new Dictionary<string, Account>();
            var list = new List<Account>();
            foreach (var entry in Accounts)
            {
                Account? parent = entry.ParentNumber != null ? byNumber[entry.ParentNumber] : null;
                var account = Account.Create(entry.Number, entry.Name, (int)entry.Type, (int)entry.Normal, parent);
                account.IsControl = entry.IsControl;
                account.SetCashFlow(entry.IsCash, entry.CashFlow.HasValue ? (int)entry.CashFlow.Value : null);
                byNumber[entry.Number] = account;
                list.Add(account);
            }
            return list;
        }
    }

    public static class ChartTemplates
    {
        public const string Simple14 = "Simple14";
        public const string Vas99 = "Vas99";

        /// <summary>
        /// Which chart a tenant is on, judged by its accounts (the platform keeps no "applied template" flag): the Vietnamese chart has account 111, the
        /// platform's 14-account chart has 1000 and 4000. A tenant with neither (its own chart) is treated as Vietnamese, as before: the statement layouts
        /// then report what they cannot place under "unmapped".
        /// </summary>
        public static string Detect(IEnumerable<ERP.ACC.Domain.Entities.Account> accounts)
        {
            var numbers = accounts.Select(a => a.AccountNumber).ToHashSet(StringComparer.Ordinal);
            return !numbers.Contains("111") && numbers.Contains("1000") && numbers.Contains("4000") ? Simple14 : Vas99;
        }

        public static IReadOnlyList<ChartTemplate> All { get; } = new[] { BuildSimple14(), BuildVas99() };

        public static ChartTemplate? Find(string? id) =>
            All.FirstOrDefault(t => string.Equals(t.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase));

        // ---- the shipped 14-account chart (unchanged behaviour) ----

        private static ChartTemplate BuildSimple14() => new(
            Simple14,
            "Simple chart (14 accounts)",
            "The platform's original flat chart: cash, receivables, inventory, payables, tax, equity, revenue and a few expense accounts.",
            DefaultChartOfAccounts.Accounts
                .Select(e => new ChartTemplate.Entry(e.Number, e.Name, e.Type, e.Normal, null, e.IsControl,
                    IsCash: e.Number == "1000",
                    CashFlow: e.Number == "1500" ? CashFlowCategory.Investing : null))
                .ToList(),
            PostingKeys.DefaultAccountNumbers);

        // ---- Vietnamese chart (Circular 99/2025/TT-BTC, which replaced Circular 200/2014 from 2026-01-01) ----
        // A suggested starting chart of the commonly used accounts (classes 1-9, level-1 plus the usual level-2 accounts), numbered as in
        // Circular 200 which the new circular largely keeps. It is NOT legal advice: the company's accountant must review the list and the
        // posting map before real use (docs/business/Accounting/ChartOfAccounts.md). Types: classes 1-2 Asset, 3 Liability, 4 Equity,
        // 5/7 Revenue, 6/8 Expense; 911 (profit determination) is a temporary Equity account that closing entries bring back to zero.

        private static ChartTemplate BuildVas99()
        {
            const AccountType A = AccountType.Asset, L = AccountType.Liability, E = AccountType.Equity, R = AccountType.Revenue, X = AccountType.Expense;
            const NormalBalance Dr = NormalBalance.Debit, Cr = NormalBalance.Credit;
            var accounts = new List<ChartTemplate.Entry>();
            void Add(string number, string name, AccountType type, NormalBalance normal, string? parent = null, bool control = false) =>
                accounts.Add(new ChartTemplate.Entry(number, name, type, normal, parent, control));

            // Class 1 - current assets
            Add("111", "Tiền mặt", A, Dr, control: true);
            Add("1111", "Tiền Việt Nam", A, Dr, "111");
            Add("1112", "Ngoại tệ", A, Dr, "111");
            Add("112", "Tiền gửi ngân hàng", A, Dr, control: true);
            Add("1121", "Tiền Việt Nam", A, Dr, "112");
            Add("1122", "Ngoại tệ", A, Dr, "112");
            Add("113", "Tiền đang chuyển", A, Dr);
            Add("121", "Chứng khoán kinh doanh", A, Dr);
            Add("128", "Đầu tư nắm giữ đến ngày đáo hạn", A, Dr);
            Add("131", "Phải thu của khách hàng", A, Dr, control: true);
            Add("133", "Thuế GTGT được khấu trừ", A, Dr);
            Add("1331", "Thuế GTGT được khấu trừ của hàng hóa, dịch vụ", A, Dr, "133");
            Add("1332", "Thuế GTGT được khấu trừ của TSCĐ", A, Dr, "133");
            Add("136", "Phải thu nội bộ", A, Dr);
            Add("138", "Phải thu khác", A, Dr);
            Add("1381", "Tài sản thiếu chờ xử lý", A, Dr, "138");
            Add("1388", "Phải thu khác", A, Dr, "138");
            Add("141", "Tạm ứng", A, Dr);
            Add("151", "Hàng mua đang đi đường", A, Dr);
            Add("152", "Nguyên liệu, vật liệu", A, Dr);
            Add("153", "Công cụ, dụng cụ", A, Dr);
            Add("154", "Chi phí sản xuất, kinh doanh dở dang", A, Dr);
            Add("155", "Thành phẩm", A, Dr);
            Add("156", "Hàng hóa", A, Dr);
            Add("1561", "Giá mua hàng hóa", A, Dr, "156");
            Add("1562", "Chi phí thu mua hàng hóa", A, Dr, "156");
            Add("157", "Hàng gửi đi bán", A, Dr);
            // Class 2 - non-current assets
            Add("211", "Tài sản cố định hữu hình", A, Dr);
            Add("212", "Tài sản cố định thuê tài chính", A, Dr);
            Add("213", "Tài sản cố định vô hình", A, Dr);
            Add("214", "Hao mòn tài sản cố định", A, Cr);
            Add("2141", "Hao mòn TSCĐ hữu hình", A, Cr, "214");
            Add("2142", "Hao mòn TSCĐ thuê tài chính", A, Cr, "214");
            Add("2143", "Hao mòn TSCĐ vô hình", A, Cr, "214");
            Add("2147", "Hao mòn bất động sản đầu tư", A, Cr, "214");
            Add("217", "Bất động sản đầu tư", A, Dr);
            Add("221", "Đầu tư vào công ty con", A, Dr);
            Add("222", "Đầu tư vào công ty liên doanh, liên kết", A, Dr);
            Add("228", "Đầu tư khác", A, Dr);
            Add("229", "Dự phòng tổn thất tài sản", A, Cr);
            Add("2291", "Dự phòng giảm giá chứng khoán kinh doanh", A, Cr, "229");
            Add("2292", "Dự phòng tổn thất đầu tư vào đơn vị khác", A, Cr, "229");
            Add("2293", "Dự phòng phải thu khó đòi", A, Cr, "229");
            Add("2294", "Dự phòng giảm giá hàng tồn kho", A, Cr, "229");
            Add("241", "Xây dựng cơ bản dở dang", A, Dr);
            Add("242", "Chi phí trả trước", A, Dr);
            Add("243", "Tài sản thuế thu nhập hoãn lại", A, Dr);
            Add("244", "Cầm cố, thế chấp, ký quỹ, ký cược", A, Dr);
            // Class 3 - liabilities
            Add("331", "Phải trả cho người bán", L, Cr, control: true);
            Add("333", "Thuế và các khoản phải nộp Nhà nước", L, Cr);
            Add("3331", "Thuế giá trị gia tăng phải nộp", L, Cr, "333");
            Add("3332", "Thuế tiêu thụ đặc biệt", L, Cr, "333");
            Add("3333", "Thuế xuất, nhập khẩu", L, Cr, "333");
            Add("3334", "Thuế thu nhập doanh nghiệp", L, Cr, "333");
            Add("3335", "Thuế thu nhập cá nhân", L, Cr, "333");
            Add("3338", "Thuế bảo vệ môi trường và các loại thuế khác", L, Cr, "333");
            Add("3339", "Phí, lệ phí và các khoản phải nộp khác", L, Cr, "333");
            Add("334", "Phải trả người lao động", L, Cr);
            Add("335", "Chi phí phải trả", L, Cr);
            Add("336", "Phải trả nội bộ", L, Cr);
            Add("337", "Thanh toán theo tiến độ kế hoạch hợp đồng xây dựng", L, Cr);
            Add("338", "Phải trả, phải nộp khác", L, Cr);
            Add("3382", "Kinh phí công đoàn", L, Cr, "338");
            Add("3383", "Bảo hiểm xã hội", L, Cr, "338");
            Add("3384", "Bảo hiểm y tế", L, Cr, "338");
            Add("3386", "Nhận ký quỹ, ký cược ngắn hạn", L, Cr, "338");
            Add("3387", "Doanh thu chưa thực hiện", L, Cr, "338");
            Add("3388", "Phải trả, phải nộp khác", L, Cr, "338");
            Add("3389", "Bảo hiểm thất nghiệp", L, Cr, "338");
            Add("341", "Vay và nợ thuê tài chính", L, Cr);
            Add("343", "Trái phiếu phát hành", L, Cr);
            Add("344", "Nhận ký quỹ, ký cược", L, Cr);
            Add("347", "Thuế thu nhập hoãn lại phải trả", L, Cr);
            Add("352", "Dự phòng phải trả", L, Cr);
            Add("353", "Quỹ khen thưởng, phúc lợi", L, Cr);
            Add("356", "Quỹ phát triển khoa học và công nghệ", L, Cr);
            // Class 4 - equity
            Add("411", "Vốn đầu tư của chủ sở hữu", E, Cr);
            Add("4111", "Vốn góp của chủ sở hữu", E, Cr, "411");
            Add("4112", "Thặng dư vốn cổ phần", E, Cr, "411");
            Add("412", "Chênh lệch đánh giá lại tài sản", E, Cr);
            Add("413", "Chênh lệch tỷ giá hối đoái", E, Cr);
            Add("414", "Quỹ đầu tư phát triển", E, Cr);
            Add("418", "Các quỹ khác thuộc vốn chủ sở hữu", E, Cr);
            Add("419", "Cổ phiếu mua lại của chính mình", E, Dr);
            Add("421", "Lợi nhuận sau thuế chưa phân phối", E, Cr);
            Add("4211", "Lợi nhuận sau thuế chưa phân phối năm trước", E, Cr, "421");
            Add("4212", "Lợi nhuận sau thuế chưa phân phối năm nay", E, Cr, "421");
            // Class 5 - revenue
            Add("511", "Doanh thu bán hàng và cung cấp dịch vụ", R, Cr);
            Add("5111", "Doanh thu bán hàng hóa", R, Cr, "511");
            Add("5112", "Doanh thu bán thành phẩm", R, Cr, "511");
            Add("5113", "Doanh thu cung cấp dịch vụ", R, Cr, "511");
            Add("5118", "Doanh thu khác", R, Cr, "511");
            Add("515", "Doanh thu hoạt động tài chính", R, Cr);
            Add("521", "Các khoản giảm trừ doanh thu", R, Dr);
            Add("5211", "Chiết khấu thương mại", R, Dr, "521");
            Add("5212", "Hàng bán bị trả lại", R, Dr, "521");
            Add("5213", "Giảm giá hàng bán", R, Dr, "521");
            // Class 6 - expenses
            Add("621", "Chi phí nguyên liệu, vật liệu trực tiếp", X, Dr);
            Add("622", "Chi phí nhân công trực tiếp", X, Dr);
            Add("627", "Chi phí sản xuất chung", X, Dr);
            Add("632", "Giá vốn hàng bán", X, Dr);
            Add("635", "Chi phí tài chính", X, Dr);
            Add("641", "Chi phí bán hàng", X, Dr);
            Add("642", "Chi phí quản lý doanh nghiệp", X, Dr);
            Add("6421", "Chi phí nhân viên quản lý", X, Dr, "642");
            Add("6422", "Chi phí vật liệu quản lý", X, Dr, "642");
            Add("6427", "Chi phí dịch vụ mua ngoài", X, Dr, "642");
            Add("6428", "Chi phí bằng tiền khác", X, Dr, "642");
            // Class 7-8 - other income / expenses, income tax
            Add("711", "Thu nhập khác", R, Cr);
            Add("811", "Chi phí khác", X, Dr);
            Add("821", "Chi phí thuế thu nhập doanh nghiệp", X, Dr);
            Add("8211", "Chi phí thuế TNDN hiện hành", X, Dr, "821");
            Add("8212", "Chi phí thuế TNDN hoãn lại", X, Dr, "821");
            // Class 9 - profit determination (temporary: closing entries leave it at zero)
            Add("911", "Xác định kết quả kinh doanh", E, Cr);

            // Cash-flow attributes (suggested; editable per account): cash and bank accounts, and the counter-accounts that are investing / financing.
            var cash = new HashSet<string> { "111", "1111", "1112", "112", "1121", "1122", "113" };
            var investing = new HashSet<string> { "121", "128", "211", "212", "213", "217", "221", "222", "228", "241" };
            var financing = new HashSet<string> { "341", "343", "344" };
            for (var i = 0; i < accounts.Count; i++)
            {
                var a = accounts[i];
                accounts[i] = a with
                {
                    IsCash = cash.Contains(a.Number),
                    CashFlow = investing.Contains(a.Number) ? CashFlowCategory.Investing : financing.Contains(a.Number) ? CashFlowCategory.Financing : null,
                };
            }

            var map = new Dictionary<string, string>
            {
                [PostingKeys.Cash] = "1111",
                [PostingKeys.Bank] = "1121",
                [PostingKeys.AccountsReceivable] = "131",
                [PostingKeys.AccountsPayable] = "331",
                [PostingKeys.SalesRevenue] = "5111",
                [PostingKeys.Inventory] = "1561",
                [PostingKeys.VatOutput] = "3331",
                [PostingKeys.GoodsReceivedNotInvoiced] = "3388",
                [PostingKeys.CostOfGoodsSold] = "632",
                [PostingKeys.InventoryAdjustment] = "632",
                [PostingKeys.CashOverShort] = "811",
                [PostingKeys.SalaryExpense] = "6421",
                [PostingKeys.SalariesPayable] = "334",
                [PostingKeys.PayrollWithholdingsPayable] = "3388",
                [PostingKeys.SalesReturns] = "5212",
                [PostingKeys.IncomeSummary] = "911",
                [PostingKeys.RetainedEarnings] = "4212",
            };

            return new ChartTemplate(
                Vas99,
                "Vietnamese chart (Circular 99/2025/TT-BTC)",
                "Commonly used accounts of classes 1-9 with sub-accounts, as a starting point for Vietnamese enterprises. Review it with your accountant.",
                accounts,
                map);
        }
    }
}

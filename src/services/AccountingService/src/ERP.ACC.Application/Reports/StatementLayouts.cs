using ERP.ACC.Domain.Enums;

namespace ERP.ACC.Application.Reports
{
    /// <summary>Which balances of an account a term takes: the net, or only a debit / credit balance (B01 splits 131 and 331 by the side of the balance).</summary>
    public enum BalanceSide { Net, DebitOnly, CreditOnly }

    public sealed record StatementTerm(string Prefix, BalanceSide Side = BalanceSide.Net);

    public sealed record FormulaPart(string Code, int Sign);

    /// <summary>
    /// One indicator row (mã số) of a statement. Account-driven: the sum of the balances of every account whose number starts with one of the
    /// <see cref="Terms"/>, taken on the row's <see cref="Natural"/> side (so a contra account reduces the group by itself). Formula-driven: a signed sum
    /// of other rows. <see cref="Special"/> is a computed source ("UnclosedResult" = revenue minus expenses not yet moved to retained earnings).
    /// </summary>
    public sealed record StatementRow(
        string Code,
        string Name,
        int Indent,
        NormalBalance Natural,
        IReadOnlyList<StatementTerm> Terms,
        IReadOnlyList<FormulaPart> Formula,
        bool IsTotal = false,
        string? Special = null);

    public sealed record StatementLayout(
        string Id,
        string Title,
        string ChartTemplate,
        IReadOnlyList<AccountType> AccountTypes,
        IReadOnlyList<StatementRow> Rows,
        IReadOnlyList<string> Notes);

    /// <summary>
    /// The Vietnamese statements as data (ROADMAP P1-2c): the balance sheet B01-DN and the income statement B02-DN for the <c>Vas99</c> chart. The rows and
    /// the account groups were written from the Circular 200 forms, whose numbering Circular 99/2025/TT-BTC largely keeps; they are a DRAFT that the company's
    /// accountant must review against the official forms before the statements are filed (docs/business/Accounting/Reports.md).
    /// </summary>
    public static class StatementLayouts
    {
        public const string BalanceSheet = "B01-DN";
        public const string IncomeStatement = "B02-DN";

        public static IReadOnlyList<StatementLayout> All { get; } = new[] { BuildB01(), BuildB02() };

        public static StatementLayout? Find(string id) => All.FirstOrDefault(l => l.Id == id);

        // ---- builders -----------------------------------------------------------------------------------------------

        private sealed class Builder
        {
            private readonly List<StatementRow> _rows = new();

            public IReadOnlyList<StatementRow> Rows => _rows;

            /// <summary>An account-driven row.</summary>
            public Builder Accounts(string code, string name, int indent, NormalBalance natural, params string[] terms)
            {
                _rows.Add(new StatementRow(code, name, indent, natural, terms.Select(Term).ToList(), Array.Empty<FormulaPart>()));
                return this;
            }

            /// <summary>A row computed from other rows: "+110 +120 -130".</summary>
            public Builder Sum(string code, string name, int indent, NormalBalance natural, string formula, bool total = false)
            {
                var parts = formula.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => new FormulaPart(p[1..], p[0] == '-' ? -1 : 1)).ToList();
                _rows.Add(new StatementRow(code, name, indent, natural, Array.Empty<StatementTerm>(), parts, total));
                return this;
            }

            public Builder Special(string code, string name, int indent, NormalBalance natural, string special)
            {
                _rows.Add(new StatementRow(code, name, indent, natural, Array.Empty<StatementTerm>(), Array.Empty<FormulaPart>(), false, special));
                return this;
            }

            // "131:D" = debit balances only, "131:C" = credit balances only
            private static StatementTerm Term(string spec)
            {
                var parts = spec.Split(':');
                var side = parts.Length > 1 ? (parts[1] == "D" ? BalanceSide.DebitOnly : BalanceSide.CreditOnly) : BalanceSide.Net;
                return new StatementTerm(parts[0], side);
            }
        }

        private const NormalBalance Dr = NormalBalance.Debit;
        private const NormalBalance Cr = NormalBalance.Credit;

        private static StatementLayout BuildB02()
        {
            var b = new Builder();
            b.Accounts("01", "1. Doanh thu bán hàng và cung cấp dịch vụ", 1, Cr, "511")
             .Accounts("02", "2. Các khoản giảm trừ doanh thu", 1, Dr, "521")
             .Sum("10", "3. Doanh thu thuần về bán hàng và cung cấp dịch vụ (10 = 01 - 02)", 1, Cr, "+01 -02", total: true)
             .Accounts("11", "4. Giá vốn hàng bán", 1, Dr, "632")
             .Sum("20", "5. Lợi nhuận gộp về bán hàng và cung cấp dịch vụ (20 = 10 - 11)", 1, Cr, "+10 -11", total: true)
             .Accounts("21", "6. Doanh thu hoạt động tài chính", 1, Cr, "515")
             .Accounts("22", "7. Chi phí tài chính", 1, Dr, "635")
             .Accounts("25", "8. Chi phí bán hàng", 1, Dr, "641")
             .Accounts("26", "9. Chi phí quản lý doanh nghiệp", 1, Dr, "642")
             .Sum("30", "10. Lợi nhuận thuần từ hoạt động kinh doanh (30 = 20 + 21 - 22 - 25 - 26)", 1, Cr, "+20 +21 -22 -25 -26", total: true)
             .Accounts("31", "11. Thu nhập khác", 1, Cr, "711")
             .Accounts("32", "12. Chi phí khác", 1, Dr, "811")
             .Sum("40", "13. Lợi nhuận khác (40 = 31 - 32)", 1, Cr, "+31 -32", total: true)
             .Sum("50", "14. Tổng lợi nhuận kế toán trước thuế (50 = 30 + 40)", 1, Cr, "+30 +40", total: true)
             .Accounts("51", "15. Chi phí thuế TNDN hiện hành", 1, Dr, "8211")
             .Accounts("52", "16. Chi phí thuế TNDN hoãn lại", 1, Dr, "8212")
             .Sum("60", "17. Lợi nhuận sau thuế thu nhập doanh nghiệp (60 = 50 - 51 - 52)", 1, Cr, "+50 -51 -52", total: true);

            return new StatementLayout(
                IncomeStatement,
                "Báo cáo kết quả hoạt động kinh doanh (Mẫu B02-DN) — bản nháp",
                ChartTemplates_Vas99,
                new[] { AccountType.Revenue, AccountType.Expense },
                b.Rows,
                new[]
                {
                    "Draft: the rows and account groups follow the Circular 200 form (numbering largely kept by Circular 99/2025); the accountant must review them before filing.",
                    "Period activity without the period-closing entries. Production-cost accounts (621, 622, 627) are not part of this form: they flow into inventory and cost of goods sold.",
                });
        }

        private static StatementLayout BuildB01()
        {
            var b = new Builder();
            // ---- assets
            b.Accounts("111", "1. Tiền", 3, Dr, "111", "112", "113")
             .Sum("110", "I. Tiền và các khoản tương đương tiền", 2, Dr, "+111", total: true)
             .Accounts("121", "1. Chứng khoán kinh doanh", 3, Dr, "121")
             .Accounts("122", "2. Dự phòng giảm giá chứng khoán kinh doanh", 3, Dr, "2291")
             .Accounts("123", "3. Đầu tư nắm giữ đến ngày đáo hạn", 3, Dr, "128")
             .Sum("120", "II. Đầu tư tài chính ngắn hạn", 2, Dr, "+121 +122 +123", total: true)
             .Accounts("131", "1. Phải thu ngắn hạn của khách hàng", 3, Dr, "131:D")
             .Accounts("132", "2. Trả trước cho người bán ngắn hạn", 3, Dr, "331:D")
             .Accounts("133", "3. Phải thu nội bộ ngắn hạn", 3, Dr, "136")
             .Accounts("136", "4. Phải thu ngắn hạn khác", 3, Dr, "138", "141")
             .Accounts("137", "5. Dự phòng phải thu ngắn hạn khó đòi", 3, Dr, "2293")
             .Sum("130", "III. Các khoản phải thu ngắn hạn", 2, Dr, "+131 +132 +133 +136 +137", total: true)
             .Accounts("141", "1. Hàng tồn kho", 3, Dr, "151", "152", "153", "154", "155", "156", "157")
             .Accounts("149", "2. Dự phòng giảm giá hàng tồn kho", 3, Dr, "2294")
             .Sum("140", "IV. Hàng tồn kho", 2, Dr, "+141 +149", total: true)
             .Accounts("152", "1. Thuế GTGT được khấu trừ", 3, Dr, "133")
             .Sum("150", "V. Tài sản ngắn hạn khác", 2, Dr, "+152", total: true)
             .Sum("100", "A. TÀI SẢN NGẮN HẠN", 1, Dr, "+110 +120 +130 +140 +150", total: true)
             .Accounts("222", "- Nguyên giá", 4, Dr, "211")
             .Accounts("223", "- Giá trị hao mòn lũy kế", 4, Dr, "2141")
             .Sum("221", "1. Tài sản cố định hữu hình", 3, Dr, "+222 +223")
             .Accounts("225", "- Nguyên giá", 4, Dr, "212")
             .Accounts("226", "- Giá trị hao mòn lũy kế", 4, Dr, "2142")
             .Sum("224", "2. Tài sản cố định thuê tài chính", 3, Dr, "+225 +226")
             .Accounts("228", "- Nguyên giá", 4, Dr, "213")
             .Accounts("229", "- Giá trị hao mòn lũy kế", 4, Dr, "2143")
             .Sum("227", "3. Tài sản cố định vô hình", 3, Dr, "+228 +229")
             .Sum("220", "II. Tài sản cố định", 2, Dr, "+221 +224 +227", total: true)
             .Accounts("231", "- Nguyên giá", 4, Dr, "217")
             .Accounts("232", "- Giá trị hao mòn lũy kế", 4, Dr, "2147")
             .Sum("230", "III. Bất động sản đầu tư", 2, Dr, "+231 +232", total: true)
             .Accounts("242", "1. Chi phí xây dựng cơ bản dở dang", 3, Dr, "241")
             .Sum("240", "IV. Tài sản dở dang dài hạn", 2, Dr, "+242", total: true)
             .Accounts("251", "1. Đầu tư vào công ty con", 3, Dr, "221")
             .Accounts("252", "2. Đầu tư vào công ty liên doanh, liên kết", 3, Dr, "222")
             .Accounts("253", "3. Đầu tư góp vốn vào đơn vị khác", 3, Dr, "228")
             .Accounts("254", "4. Dự phòng đầu tư tài chính dài hạn", 3, Dr, "2292")
             .Sum("250", "V. Đầu tư tài chính dài hạn", 2, Dr, "+251 +252 +253 +254", total: true)
             .Accounts("261", "1. Chi phí trả trước dài hạn", 3, Dr, "242")
             .Accounts("262", "2. Tài sản thuế thu nhập hoãn lại", 3, Dr, "243")
             .Accounts("268", "3. Tài sản dài hạn khác", 3, Dr, "244")
             .Sum("260", "VI. Tài sản dài hạn khác", 2, Dr, "+261 +262 +268", total: true)
             .Sum("200", "B. TÀI SẢN DÀI HẠN", 1, Dr, "+220 +230 +240 +250 +260", total: true)
             .Sum("270", "TỔNG CỘNG TÀI SẢN (270 = 100 + 200)", 1, Dr, "+100 +200", total: true)
             // ---- liabilities
             .Accounts("311", "1. Phải trả người bán ngắn hạn", 3, Cr, "331:C")
             .Accounts("312", "2. Người mua trả tiền trước ngắn hạn", 3, Cr, "131:C")
             .Accounts("313", "3. Thuế và các khoản phải nộp Nhà nước", 3, Cr, "333")
             .Accounts("314", "4. Phải trả người lao động", 3, Cr, "334")
             .Accounts("315", "5. Chi phí phải trả ngắn hạn", 3, Cr, "335")
             .Accounts("316", "6. Phải trả nội bộ ngắn hạn", 3, Cr, "336")
             .Accounts("317", "7. Phải trả theo tiến độ kế hoạch hợp đồng xây dựng", 3, Cr, "337")
             .Accounts("318", "8. Doanh thu chưa thực hiện ngắn hạn", 3, Cr, "3387")
             .Accounts("319", "9. Phải trả ngắn hạn khác", 3, Cr, "3381", "3382", "3383", "3384", "3385", "3386", "3388", "3389")
             .Accounts("320", "10. Vay và nợ thuê tài chính ngắn hạn", 3, Cr, "341")
             .Accounts("321", "11. Dự phòng phải trả ngắn hạn", 3, Cr, "352")
             .Accounts("322", "12. Quỹ khen thưởng, phúc lợi", 3, Cr, "353")
             .Sum("310", "I. Nợ ngắn hạn", 2, Cr, "+311 +312 +313 +314 +315 +316 +317 +318 +319 +320 +321 +322", total: true)
             .Accounts("337", "1. Phải trả dài hạn khác", 3, Cr, "344")
             .Accounts("338", "2. Vay và nợ thuê tài chính dài hạn", 3, Cr, "343")
             .Accounts("340", "3. Thuế thu nhập hoãn lại phải trả", 3, Cr, "347")
             .Accounts("343", "4. Quỹ phát triển khoa học và công nghệ", 3, Cr, "356")
             .Sum("330", "II. Nợ dài hạn", 2, Cr, "+337 +338 +340 +343", total: true)
             .Sum("300", "C. NỢ PHẢI TRẢ", 1, Cr, "+310 +330", total: true)
             // ---- equity
             .Accounts("411", "1. Vốn góp của chủ sở hữu", 3, Cr, "4111")
             .Accounts("412", "2. Thặng dư vốn cổ phần", 3, Cr, "4112")
             .Accounts("415", "3. Cổ phiếu quỹ", 3, Cr, "419")
             .Accounts("416", "4. Chênh lệch đánh giá lại tài sản", 3, Cr, "412")
             .Accounts("417", "5. Chênh lệch tỷ giá hối đoái", 3, Cr, "413")
             .Accounts("418", "6. Quỹ đầu tư phát triển", 3, Cr, "414")
             .Accounts("419", "7. Quỹ khác thuộc vốn chủ sở hữu", 3, Cr, "418")
             .Accounts("421a", "- Lợi nhuận sau thuế chưa phân phối lũy kế đến cuối kỳ trước", 4, Cr, "4211")
             .Sum("421b", "- Lợi nhuận sau thuế chưa phân phối kỳ này", 4, Cr, "")
             .Sum("421", "8. Lợi nhuận sau thuế chưa phân phối", 3, Cr, "+421a +421b")
             .Sum("410", "I. Vốn chủ sở hữu", 2, Cr, "+411 +412 +415 +416 +417 +418 +419 +421", total: true)
             .Sum("400", "D. VỐN CHỦ SỞ HỮU", 1, Cr, "+410", total: true)
             .Sum("440", "TỔNG CỘNG NGUỒN VỐN (440 = 300 + 400)", 1, Cr, "+300 +400", total: true);

            // 421b = the retained-earnings account of this year + the result not yet closed (computed)
            var rows = b.Rows.ToList();
            var i421b = rows.FindIndex(r => r.Code == "421b");
            rows[i421b] = new StatementRow("421b", "- Lợi nhuận sau thuế chưa phân phối kỳ này", 4, Cr,
                new[] { new StatementTerm("4212") }, Array.Empty<FormulaPart>(), false, "UnclosedResult");

            return new StatementLayout(
                BalanceSheet,
                "Bảng cân đối kế toán (Mẫu B01-DN) — bản nháp",
                ChartTemplates_Vas99,
                new[] { AccountType.Asset, AccountType.Liability, AccountType.Equity },
                rows,
                new[]
                {
                    "Draft: the rows and account groups follow the Circular 200 form (numbering largely kept by Circular 99/2025); the accountant must review them before filing.",
                    "There is no maturity data, so short- and long-term are decided by the account: borrowings 341 are short-term, bonds 343 and deposits received 344 long-term; prepaid expenses 242 are long-term.",
                    "Receivables and payables are split by the side of each account's balance: a credit balance on 131 is shown under customers' advances (312), a debit balance on 331 under advances to suppliers (132).",
                    "Row 421b includes the result of revenue and expense accounts that no closing entry has moved yet, so the sheet balances before the period is closed.",
                });
        }

        private const string ChartTemplates_Vas99 = "Vas99";
    }
}

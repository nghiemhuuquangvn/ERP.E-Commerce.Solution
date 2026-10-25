using ERP.ACC.Domain.Enums;

namespace ERP.ACC.Application.Reports
{
    /// <summary>A counter-account prefix and the direction of the cash it moves (cash received = inflow, cash paid = outflow).</summary>
    public sealed record CashFlowTerm(string Prefix, bool Inflow);

    /// <summary>
    /// One indicator row (mã số) of the cash-flow statement. Account-driven: takes the cash movements whose counter-account matches a term (the first
    /// matching row of the layout wins, so a movement counts once). A fallback row takes the movements of an activity category that no term took
    /// (operating: "other receipts / payments"). Formula-driven: a signed sum of other rows. <see cref="Special"/>: "OpeningCash", "FxEffect".
    /// </summary>
    public sealed record CashFlowRow(
        string Code,
        string Name,
        int Indent,
        IReadOnlyList<CashFlowTerm> Terms,
        IReadOnlyList<FormulaPart> Formula,
        bool IsTotal = false,
        string? Special = null,
        CashFlowCategory? FallbackCategory = null,
        bool FallbackInflow = false);

    public sealed record CashFlowLayout(string Id, string Title, IReadOnlyList<CashFlowRow> Rows, IReadOnlyList<string> Notes, string ChartTemplate = "Vas99");

    /// <summary>
    /// The cash-flow statement B03-DN (direct method) as data (ROADMAP P1-2d) for the <c>Vas99</c> chart. Like B01 / B02 it is a DRAFT written from the
    /// Circular 200 form until the company's accountant has reviewed it (docs/business/Accounting/Reports.md).
    /// </summary>
    public static class CashFlowLayouts
    {
        public const string CashFlow = "B03-DN";

        public static CashFlowLayout Layout { get; } = Build();

        /// <summary>B03-DN for the platform's 14-account chart (<c>Simple14</c>), same row codes where a row exists.</summary>
        public static CashFlowLayout Simple14Layout { get; } = BuildSimple14();

        /// <summary>The layout for a chart (<see cref="ERP.ACC.Application.Posting.ChartTemplates.Detect"/>).</summary>
        public static CashFlowLayout For(string chartTemplate) => chartTemplate == "Simple14" ? Simple14Layout : Layout;

        private static CashFlowLayout BuildSimple14()
        {
            var b = new Builder();
            b.In("01", "1. Tiền thu từ bán hàng, cung cấp dịch vụ và doanh thu khác", 1, "4000", "4100", "1100")
             .Out("02", "2. Tiền chi trả cho người cung cấp hàng hóa và dịch vụ, chi phí hoạt động", 1, "2000", "1200", "5000", "5100")
             .Out("03", "3. Tiền chi trả cho người lao động", 1, "5200", "2100")
             .Out("05", "5. Thuế và các khoản đã nộp Nhà nước", 1, "2200")
             .Fallback("06", "6. Tiền thu khác từ hoạt động kinh doanh", 1, CashFlowCategory.Operating, inflow: true)
             .Fallback("07", "7. Tiền chi khác cho hoạt động kinh doanh", 1, CashFlowCategory.Operating, inflow: false)
             .Sum("20", "Lưu chuyển tiền thuần từ hoạt động kinh doanh (20 = 01 + 02 + 03 + 05 + 06 + 07)", 0, "+01 +02 +03 +05 +06 +07")
             .Out("21", "1. Tiền chi để mua sắm, xây dựng TSCĐ", 1, "1500")
             .In("22", "2. Tiền thu từ thanh lý, nhượng bán TSCĐ", 1, "1500")
             .Sum("30", "Lưu chuyển tiền thuần từ hoạt động đầu tư (30 = 21 + 22)", 0, "+21 +22")
             .In("31", "1. Tiền thu từ nhận vốn góp của chủ sở hữu", 1, "3000")
             .Out("32", "2. Tiền trả lại vốn góp cho chủ sở hữu", 1, "3000")
             .Out("36", "6. Lợi nhuận đã trả cho chủ sở hữu", 1, "3100")
             .Sum("40", "Lưu chuyển tiền thuần từ hoạt động tài chính (40 = 31 + 32 + 36)", 0, "+31 +32 +36")
             .Sum("50", "Lưu chuyển tiền thuần trong kỳ (50 = 20 + 30 + 40)", 0, "+20 +30 +40")
             .Special("60", "Tiền và tương đương tiền đầu kỳ", 0, "OpeningCash")
             .Special("61", "Ảnh hưởng của thay đổi tỷ giá hối đoái quy đổi ngoại tệ", 0, "FxEffect")
             .Sum("70", "Tiền và tương đương tiền cuối kỳ (70 = 50 + 60 + 61)", 0, "+50 +60 +61");

            return new CashFlowLayout(CashFlow, "Báo cáo lưu chuyển tiền tệ (B03-DN) — phương pháp trực tiếp, biểu đồ 14 tài khoản", b.Rows, new[]
            {
                "Draft layout for the platform's 14-account chart (no interest, borrowing or foreign-currency accounts exist). Apply the Vietnamese chart for the full form.",
                "Outflows are negative. Each cash movement is classified by the counter-account of its journal entry; transfers between cash accounts are left out. Payments on 2100 (accrued liabilities, also used for salaries payable) are shown as paid to employees.",
                "Row 61 (foreign-exchange effect) is zero: the ledger has no foreign currency yet.",
            }, "Simple14");
        }

        private sealed class Builder
        {
            private readonly List<CashFlowRow> _rows = new();

            public IReadOnlyList<CashFlowRow> Rows => _rows;

            public Builder In(string code, string name, int indent, params string[] prefixes) => Add(code, name, indent, prefixes, true);

            public Builder Out(string code, string name, int indent, params string[] prefixes) => Add(code, name, indent, prefixes, false);

            /// <summary>Cash received on accounts for the operation and paid on others (loans, capital, ...): both directions.</summary>
            private Builder Add(string code, string name, int indent, string[] prefixes, bool inflow)
            {
                _rows.Add(new CashFlowRow(code, name, indent, prefixes.Select(p => new CashFlowTerm(p, inflow)).ToList(), Array.Empty<FormulaPart>()));
                return this;
            }

            public Builder Fallback(string code, string name, int indent, CashFlowCategory category, bool inflow)
            {
                _rows.Add(new CashFlowRow(code, name, indent, Array.Empty<CashFlowTerm>(), Array.Empty<FormulaPart>(), false, null, category, inflow));
                return this;
            }

            public Builder Sum(string code, string name, int indent, string formula, bool total = true)
            {
                var parts = formula.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => new FormulaPart(p[1..], p[0] == '-' ? -1 : 1)).ToList();
                _rows.Add(new CashFlowRow(code, name, indent, Array.Empty<CashFlowTerm>(), parts, total));
                return this;
            }

            public Builder Special(string code, string name, int indent, string special)
            {
                _rows.Add(new CashFlowRow(code, name, indent, Array.Empty<CashFlowTerm>(), Array.Empty<FormulaPart>(), false, special));
                return this;
            }
        }

        private static CashFlowLayout Build()
        {
            var b = new Builder();
            b.In("01", "1. Tiền thu từ bán hàng, cung cấp dịch vụ và doanh thu khác", 1, "511", "131", "3331", "3387")
             .Out("02", "2. Tiền chi trả cho người cung cấp hàng hóa và dịch vụ", 1,
                  "331", "151", "152", "153", "154", "155", "156", "157", "158", "133", "611", "621", "627", "641", "642")
             .Out("03", "3. Tiền chi trả cho người lao động", 1, "334")
             .Out("04", "4. Tiền lãi vay đã trả", 1, "635", "335")
             .Out("05", "5. Thuế thu nhập doanh nghiệp đã nộp", 1, "3334", "8211")
             .Fallback("06", "6. Tiền thu khác từ hoạt động kinh doanh", 1, CashFlowCategory.Operating, inflow: true)
             .Fallback("07", "7. Tiền chi khác cho hoạt động kinh doanh", 1, CashFlowCategory.Operating, inflow: false)
             .Sum("20", "Lưu chuyển tiền thuần từ hoạt động kinh doanh (20 = 01 + ... + 07)", 0, "+01 +02 +03 +04 +05 +06 +07")
             .Out("21", "1. Tiền chi để mua sắm, xây dựng TSCĐ và các tài sản dài hạn khác", 1, "211", "213", "217", "241")
             .In("22", "2. Tiền thu từ thanh lý, nhượng bán TSCĐ và các tài sản dài hạn khác", 1, "211", "213", "217")
             .Out("23", "3. Tiền chi cho vay, mua các công cụ nợ của đơn vị khác", 1, "128")
             .In("24", "4. Tiền thu hồi cho vay, bán lại các công cụ nợ của đơn vị khác", 1, "128")
             .Out("25", "5. Tiền chi đầu tư góp vốn vào đơn vị khác", 1, "221", "222", "228")
             .In("26", "6. Tiền thu hồi đầu tư góp vốn vào đơn vị khác", 1, "221", "222", "228")
             .In("27", "7. Tiền thu lãi cho vay, cổ tức và lợi nhuận được chia", 1, "515")
             .Sum("30", "Lưu chuyển tiền thuần từ hoạt động đầu tư (30 = 21 + ... + 27)", 0, "+21 +22 +23 +24 +25 +26 +27")
             .In("31", "1. Tiền thu từ phát hành cổ phiếu, nhận vốn góp của chủ sở hữu", 1, "411")
             .Out("32", "2. Tiền trả lại vốn góp cho các chủ sở hữu, mua lại cổ phiếu của doanh nghiệp đã phát hành", 1, "411", "419")
             .In("33", "3. Tiền thu từ đi vay", 1, "341", "343", "344")
             .Out("34", "4. Tiền trả nợ gốc vay", 1, "341", "343", "344")
             .Out("35", "5. Tiền trả nợ gốc thuê tài chính", 1, "342")
             .Out("36", "6. Cổ tức, lợi nhuận đã trả cho chủ sở hữu", 1, "421")
             .Sum("40", "Lưu chuyển tiền thuần từ hoạt động tài chính (40 = 31 + ... + 36)", 0, "+31 +32 +33 +34 +35 +36")
             .Sum("50", "Lưu chuyển tiền thuần trong kỳ (50 = 20 + 30 + 40)", 0, "+20 +30 +40")
             .Special("60", "Tiền và tương đương tiền đầu kỳ", 0, "OpeningCash")
             .Special("61", "Ảnh hưởng của thay đổi tỷ giá hối đoái quy đổi ngoại tệ", 0, "FxEffect")
             .Sum("70", "Tiền và tương đương tiền cuối kỳ (70 = 50 + 60 + 61)", 0, "+50 +60 +61");

            return new CashFlowLayout(CashFlow, "Báo cáo lưu chuyển tiền tệ (B03-DN) — phương pháp trực tiếp", b.Rows, new[]
            {
                "Draft layout: written from the Circular 200 form (numbering largely kept by Circular 99/2025/TT-BTC); the company's accountant must review it before filing.",
                "Outflows are negative. Each cash movement is classified by the counter-account of its journal entry; transfers between cash accounts are left out.",
                "Row 61 (foreign-exchange effect) is zero: the ledger has no foreign currency yet.",
            });
        }
    }
}

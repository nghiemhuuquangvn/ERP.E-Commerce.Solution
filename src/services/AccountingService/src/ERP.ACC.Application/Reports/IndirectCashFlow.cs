using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;

namespace ERP.ACC.Application.Reports
{
    /// <summary>Where an indirect-method row takes its amount from.</summary>
    public enum IndirectSource
    {
        /// <summary>Profit before tax: revenue minus expenses of the range, income tax (821x) left out; closing entries never count.</summary>
        ProfitBeforeTax,
        /// <summary>Increase of the credit balance over the range (credit minus debit, end minus start) of the prefixes: depreciation, provisions, payables.</summary>
        CreditIncrease,
        /// <summary>Minus the increase of the debit balance over the range: receivables, inventories, prepaid expenses (a rise uses cash).</summary>
        DebitIncreaseNegated,
        /// <summary>Expense of the range on the prefixes (added back): interest expense.</summary>
        ExpenseAddBack,
        /// <summary>Minus the income of the range on the prefixes (it is shown in investing): interest received.</summary>
        IncomeRemoved,
        /// <summary>The cash row of the direct method with this code (rows that are cash movements by nature).</summary>
        Direct,
        /// <summary>Not derived (0): shown for the form's sake, see the note of the row.</summary>
        NotUsed,
        Formula,
        OpeningCash,
        FxEffect,
    }

    public sealed record IndirectRow(
        string Code,
        string Name,
        int Indent,
        IndirectSource Source,
        IReadOnlyList<string> Prefixes,
        IReadOnlyList<string> Except,
        IReadOnlyList<FormulaPart> Formula,
        bool IsTotal = false);

    public sealed record IndirectCashFlowLayout(string Id, string Title, IReadOnlyList<IndirectRow> Rows, IReadOnlyList<string> CoveredPrefixes, IReadOnlyList<string> Notes);

    /// <summary>
    /// The cash-flow statement B03-DN by the **indirect method** (ROADMAP P1-2g) for the <c>Vas99</c> chart: operating cash flow starts from profit before tax,
    /// adds back non-cash items, follows the changes of working capital and subtracts interest and income tax paid; investing and financing are the cash rows of the
    /// direct method. A DRAFT written from the Circular 200 form until the accountant has reviewed it.
    /// </summary>
    public static class IndirectCashFlowLayouts
    {
        public const string Id = "B03-DN-GT";

        private static readonly string[] None = Array.Empty<string>();

        public static IndirectCashFlowLayout Layout { get; } = Build();

        private sealed class Builder
        {
            private readonly List<IndirectRow> _rows = new();
            public IReadOnlyList<IndirectRow> Rows => _rows;

            public Builder Row(string code, string name, int indent, IndirectSource source, string[] prefixes, string[]? except = null)
            {
                _rows.Add(new IndirectRow(code, name, indent, source, prefixes, except ?? None, Array.Empty<FormulaPart>()));
                return this;
            }

            public Builder Sum(string code, string name, int indent, string formula)
            {
                var parts = formula.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(p => new FormulaPart(p[1..], p[0] == '-' ? -1 : 1)).ToList();
                _rows.Add(new IndirectRow(code, name, indent, IndirectSource.Formula, None, None, parts, true));
                return this;
            }

            public Builder Direct(string code, string name, int indent) => Row(code, name, indent, IndirectSource.Direct, None);
        }

        private static IndirectCashFlowLayout Build()
        {
            var b = new Builder();
            b.Row("01", "1. Lợi nhuận trước thuế", 1, IndirectSource.ProfitBeforeTax, None)
             .Row("02", "2. Khấu hao tài sản cố định và bất động sản đầu tư", 1, IndirectSource.CreditIncrease, new[] { "214" })
             .Row("03", "3. Các khoản dự phòng", 1, IndirectSource.CreditIncrease, new[] { "229" })
             .Row("04", "4. Lãi, lỗ chênh lệch tỷ giá hối đoái do đánh giá lại các khoản mục tiền tệ có gốc ngoại tệ", 1, IndirectSource.FxEffect, None)
             .Row("05", "5. Lãi, lỗ từ hoạt động đầu tư", 1, IndirectSource.IncomeRemoved, new[] { "515" })
             .Row("06", "6. Chi phí lãi vay", 1, IndirectSource.ExpenseAddBack, new[] { "635" })
             .Sum("08", "7. Lợi nhuận từ hoạt động kinh doanh trước thay đổi vốn lưu động (08 = 01 + … + 06)", 1, "+01 +02 +03 +04 +05 +06")
             .Row("09", "8. Tăng, giảm các khoản phải thu", 1, IndirectSource.DebitIncreaseNegated, new[] { "131", "133", "136", "138", "141", "244" })
             .Row("10", "9. Tăng, giảm hàng tồn kho", 1, IndirectSource.DebitIncreaseNegated, new[] { "151", "152", "153", "154", "155", "156", "157", "158" })
             .Row("11", "10. Tăng, giảm các khoản phải trả (không kể lãi vay phải trả, thuế TNDN phải nộp)", 1, IndirectSource.CreditIncrease,
                  new[] { "331", "333", "334", "338" }, new[] { "3334" })
             .Row("12", "11. Tăng, giảm chi phí trả trước", 1, IndirectSource.DebitIncreaseNegated, new[] { "142", "242" })
             .Row("13", "12. Tăng, giảm chứng khoán kinh doanh (ghi ở hoạt động đầu tư)", 1, IndirectSource.NotUsed, None)
             .Direct("14", "13. Tiền lãi vay đã trả", 1)
             .Direct("15", "14. Thuế thu nhập doanh nghiệp đã nộp", 1)
             .Row("16", "15. Tiền thu khác từ hoạt động kinh doanh", 1, IndirectSource.NotUsed, None)
             .Row("17", "16. Tiền chi khác cho hoạt động kinh doanh", 1, IndirectSource.NotUsed, None)
             .Sum("20", "Lưu chuyển tiền thuần từ hoạt động kinh doanh (20 = 08 + 09 + … + 17)", 0, "+08 +09 +10 +11 +12 +13 +14 +15 +16 +17")
             .Direct("21", "1. Tiền chi để mua sắm, xây dựng TSCĐ và các tài sản dài hạn khác", 1)
             .Direct("22", "2. Tiền thu từ thanh lý, nhượng bán TSCĐ và các tài sản dài hạn khác", 1)
             .Direct("23", "3. Tiền chi cho vay, mua các công cụ nợ của đơn vị khác", 1)
             .Direct("24", "4. Tiền thu hồi cho vay, bán lại các công cụ nợ của đơn vị khác", 1)
             .Direct("25", "5. Tiền chi đầu tư góp vốn vào đơn vị khác", 1)
             .Direct("26", "6. Tiền thu hồi đầu tư góp vốn vào đơn vị khác", 1)
             .Direct("27", "7. Tiền thu lãi cho vay, cổ tức và lợi nhuận được chia", 1)
             .Sum("30", "Lưu chuyển tiền thuần từ hoạt động đầu tư (30 = 21 + … + 27)", 0, "+21 +22 +23 +24 +25 +26 +27")
             .Direct("31", "1. Tiền thu từ phát hành cổ phiếu, nhận vốn góp của chủ sở hữu", 1)
             .Direct("32", "2. Tiền trả lại vốn góp cho các chủ sở hữu, mua lại cổ phiếu của doanh nghiệp đã phát hành", 1)
             .Direct("33", "3. Tiền thu từ đi vay", 1)
             .Direct("34", "4. Tiền trả nợ gốc vay", 1)
             .Direct("35", "5. Tiền trả nợ gốc thuê tài chính", 1)
             .Direct("36", "6. Cổ tức, lợi nhuận đã trả cho chủ sở hữu", 1)
             .Sum("40", "Lưu chuyển tiền thuần từ hoạt động tài chính (40 = 31 + … + 36)", 0, "+31 +32 +33 +34 +35 +36")
             .Sum("50", "Lưu chuyển tiền thuần trong kỳ (50 = 20 + 30 + 40)", 0, "+20 +30 +40")
             .Row("60", "Tiền và tương đương tiền đầu kỳ", 0, IndirectSource.OpeningCash, None)
             .Row("61", "Ảnh hưởng của thay đổi tỷ giá hối đoái quy đổi ngoại tệ", 0, IndirectSource.FxEffect, None)
             .Sum("70", "Tiền và tương đương tiền cuối kỳ (70 = 50 + 60 + 61)", 0, "+50 +60 +61");

            // balance-sheet accounts the form deals with (in operating rows, in the investing / financing cash rows, or on purpose left out): any other account that moved is reported
            var covered = new[]
            {
                "214", "229", "131", "133", "136", "138", "141", "244", "15", "142", "242", "331", "333", "334", "338", "335",
                "121", "128", "211", "212", "213", "217", "221", "222", "228", "241", "411", "412", "418", "419", "421", "911", "341", "342", "343", "344",
            };

            return new IndirectCashFlowLayout(Id, "Báo cáo lưu chuyển tiền tệ (B03-DN) — phương pháp gián tiếp", b.Rows, covered, new[]
            {
                "Draft layout (indirect method): written from the Circular 200 form; the company's accountant must review it before filing.",
                "Operating cash flow starts from profit before tax (revenue and expenses of the range, income tax 821x left out). Rows 02 / 03 are the rise of accumulated depreciation (214) and provisions (229); 09 – 12 are the changes of receivables, inventories, payables and prepaid expenses between the day before the range and its end.",
                "Rows 14 and 15 (interest and income tax paid) and rows 21 – 36 are the cash movements of the direct method. Rows 13, 16 and 17 are not derived: securities are shown in investing, and other operating cash is already inside profit and working capital.",
                "The indirect operating total (20) is checked against the direct method; a non-cash transaction (e.g. equipment bought on credit) or an account the form does not take shows up as a difference.",
            });
        }
    }
}

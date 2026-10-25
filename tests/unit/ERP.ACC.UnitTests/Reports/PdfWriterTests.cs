using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Reports;
using FluentAssertions;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using PdfSharp.Pdf.IO;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-2f: the PDF of a statement — real PDF bytes, Vietnamese text with the embedded font, same figures as the other exports.</summary>
public class PdfWriterTests
{
    private static FinancialStatementDto Statement(bool prior = false, bool header = true, int rows = 3)
    {
        var s = new FinancialStatementDto
        {
            LayoutId = "B02-DN", Title = "Báo cáo kết quả hoạt động kinh doanh (Mẫu B02-DN)", FromDate = new DateTime(2026, 10, 1), ToDate = new DateTime(2026, 10, 31),
            Unit = "thousand VND", IsBalanced = null,
            Notes = { "Draft layout" },
            Unmapped = { new UnmappedAccountDto { AccountNumber = "627", AccountName = "Chi phí sản xuất chung", Balance = 70m } },
        };
        for (var i = 0; i < rows; i++)
            s.Rows.Add(new StatementRowDto
            {
                Code = (i + 1).ToString("00"), Name = i == 0 ? "1. Doanh thu bán hàng và cung cấp dịch vụ" : "Chỉ tiêu " + i, Indent = i == 0 ? 1 : 2, IsTotal = i == rows - 1,
                Amount = i == 1 ? -1500m : 1234567m, PriorAmount = prior ? 800m : null,
            });
        if (header)
            s.Header = new ReportHeaderDto
            {
                CompanyName = "Công ty TNHH Mẫu", TaxCode = "0312345678", Address = "1 Lê Lợi, Quận 1", PreparedBy = "Nguyễn Văn A", ChiefAccountant = "Trần Thị B", Director = "Lê Văn C",
            };
        return s;
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

    private static List<string> AllText(FinancialStatementDto s) => PdfWriter.Build(s).Sections.Cast<Section>().SelectMany(sec => Texts(sec)).ToList();

    [Fact]
    public void The_output_is_a_real_pdf_with_vietnamese_text_and_the_embedded_font()
    {
        var bytes = PdfWriter.ToBytes(Statement());

        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        bytes.Length.Should().BeGreaterThan(20_000); // the Noto Sans subset is embedded
        using var stream = new MemoryStream(bytes);
        PdfReader.Open(stream, PdfDocumentOpenMode.Import).PageCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void The_page_carries_the_company_block_title_unit_rows_and_signatories()
    {
        var text = AllText(Statement());

        text.Should().Contain("Công ty TNHH Mẫu").And.Contain("Mã số thuế: 0312345678").And.Contain("Địa chỉ: 1 Lê Lợi, Quận 1");
        text.Should().Contain("Báo cáo kết quả hoạt động kinh doanh (Mẫu B02-DN)");
        text.Should().Contain(t => t.Contains("Đơn vị tính: nghìn đồng") && t.Contains("01/10/2026") && t.Contains("31/10/2026"));
        text.Should().Contain("1. Doanh thu bán hàng và cung cấp dịch vụ");
        text.Should().Contain("1.234.567").And.Contain("(1.500)");
        text.Should().Contain("Người lập biểu").And.Contain("Kế toán trưởng").And.Contain("Giám đốc");
        text.Should().Contain("Nguyễn Văn A").And.Contain("Trần Thị B").And.Contain("Lê Văn C");
        text.Should().Contain(t => t.StartsWith("Chưa ánh xạ: 627"));
        text.Should().Contain("Ghi chú: Draft layout");
    }

    [Fact]
    public void The_prior_column_appears_only_with_a_comparison()
    {
        var without = AllText(Statement(prior: false));
        var with = AllText(Statement(prior: true));

        without.Should().NotContain("Số kỳ trước");
        with.Should().Contain("Số kỳ trước").And.Contain("800");
    }

    [Fact]
    public void Without_a_header_there_is_no_company_block_and_no_signatories()
    {
        var text = AllText(Statement(header: false));

        text.Should().NotContain(t => t.StartsWith("Mã số thuế")).And.NotContain("Người lập biểu");
        text.Should().Contain("Báo cáo kết quả hoạt động kinh doanh (Mẫu B02-DN)");
    }

    [Fact]
    public void A_long_statement_spans_pages_and_repeats_the_table_header()
    {
        var doc = PdfWriter.Build(Statement(rows: 120));
        var table = doc.Sections.Cast<Section>().SelectMany(s => s.Elements.OfType<Table>()).First();

        table.Rows.Cast<Row>().First().HeadingFormat.Should().BeTrue();
        using var stream = new MemoryStream(PdfWriter.ToBytes(Statement(rows: 120)));
        PdfReader.Open(stream, PdfDocumentOpenMode.Import).PageCount.Should().BeGreaterThan(1);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(999, "999")]
    [InlineData(1234567, "1.234.567")]
    [InlineData(-1500, "(1.500)")]
    public void Amounts_use_the_vietnamese_thousands_separator_and_parentheses_for_negatives(int value, string expected) =>
        PdfWriter.FormatAmount(value).Should().Be(expected);

    [Fact]
    public void A_fraction_is_shown_with_a_decimal_comma()
    {
        PdfWriter.FormatAmount(1234.5m).Should().Be("1.234,50");
        PdfWriter.FormatAmount(-0.25m).Should().Be("(0,25)");
    }

    [Fact]
    public void A_real_balance_sheet_from_the_engine_renders()
    {
        var chart = ChartTemplates.Find(ChartTemplates.Vas99)!.BuildAccounts();
        var cash = chart.Single(a => a.AccountNumber == "1111");
        var equity = chart.Single(a => a.AccountNumber == "4111");
        var balances = new Dictionary<Guid, AccountBalanceSummary>
        {
            [cash.Id] = new() { AccountId = cash.Id, TotalDebit = 5_000_000m },
            [equity.Id] = new() { AccountId = equity.Id, TotalCredit = 5_000_000m },
        };
        var s = StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.BalanceSheet)!, chart, balances, null, 1000m);
        s.ToDate = new DateTime(2026, 10, 31);

        var bytes = PdfWriter.ToBytes(s);

        using var stream = new MemoryStream(bytes);
        PdfReader.Open(stream, PdfDocumentOpenMode.Import).PageCount.Should().BeGreaterThanOrEqualTo(2); // B01 has ~100 rows
        AllText(s).Should().Contain("5.000");
    }
}

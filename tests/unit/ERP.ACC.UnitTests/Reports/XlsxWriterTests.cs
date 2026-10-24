using ClosedXML.Excel;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Reports;
using FluentAssertions;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-2d: the Excel export has numeric amount cells, safe text cells and a frozen header.</summary>
public class XlsxWriterTests
{
    private static XLWorkbook Open(byte[] bytes) => new(new MemoryStream(bytes));

    private static FinancialStatementDto Statement() => new()
    {
        LayoutId = "B02-DN", Title = "Income statement", FromDate = new DateTime(2026, 10, 1), ToDate = new DateTime(2026, 10, 31),
        Rows = { new StatementRowDto { Code = "01", Name = "Doanh thu =x", Indent = 1, Amount = 1234.5m, PriorAmount = 800m } },
        Unmapped = { new UnmappedAccountDto { AccountNumber = "627", AccountName = "Overheads", Balance = 70m } },
    };

    [Fact]
    public void Amounts_are_numeric_cells_and_vietnamese_text_survives()
    {
        using var book = Open(XlsxWriter.ToBytes(ReportTables.Statement(Statement())));
        var sheet = book.Worksheet(1);

        sheet.Name.Should().Be("B02-DN");
        sheet.Cell(4, 3).DataType.Should().Be(XLDataType.Number);
        sheet.Cell(4, 3).GetDouble().Should().Be(1234.5);
        sheet.Cell(4, 4).GetDouble().Should().Be(800);
        sheet.Cell(4, 1).GetString().Should().Be("01"); // codes stay text (no leading-zero loss)
        sheet.Cell(1, 1).GetString().Should().Be("Income statement");
    }

    [Fact]
    public void Text_starting_with_a_formula_character_is_stored_as_text_with_the_guard()
    {
        using var book = Open(XlsxWriter.ToBytes(ReportTables.Statement(Statement())));
        var cell = book.Worksheet(1).Cell(4, 2);

        cell.DataType.Should().Be(XLDataType.Text);
        cell.HasFormula.Should().BeFalse();
        cell.GetString().Should().Be("  Doanh thu =x"); // starts with spaces: no guard needed, nothing evaluated

        var guarded = XlsxWriter.ToBytes(new ReportTable("t", new IReadOnlyList<object?>[] { new object?[] { "=1+1", "+84 9", "-cmd" } }, 0));
        using var book2 = Open(guarded);
        var row = book2.Worksheet(1).Row(1);
        row.Cell(1).HasFormula.Should().BeFalse();
        // Excel's own quote prefix: the apostrophe is stored as a style flag, the cell stays text and is never evaluated
        row.Cell(1).GetString().Should().Be("=1+1");
        row.Cell(1).Style.IncludeQuotePrefix.Should().BeTrue();
        row.Cell(2).GetString().Should().Be("+84 9");
        row.Cell(2).Style.IncludeQuotePrefix.Should().BeTrue();
        row.Cell(3).Style.IncludeQuotePrefix.Should().BeTrue();
    }

    [Fact]
    public void The_header_row_is_bold_and_frozen_and_the_sheet_name_is_valid()
    {
        var bytes = XlsxWriter.ToBytes(new ReportTable("A very long sheet name: [with] /bad\\ chars? * and more", new IReadOnlyList<object?>[]
        {
            new object?[] { "h1", "h2" }, new object?[] { "a", 1m },
        }, 1));
        using var book = Open(bytes);
        var sheet = book.Worksheet(1);

        sheet.Name.Length.Should().BeLessOrEqualTo(31);
        sheet.Name.Should().NotContainAny(":", "\\", "/", "?", "*", "[", "]");
        sheet.Row(1).Style.Font.Bold.Should().BeTrue();
        sheet.SheetView.SplitRow.Should().Be(1);
    }

    [Fact]
    public void Csv_and_excel_come_from_the_same_table()
    {
        var table = ReportTables.Statement(Statement());
        var csv = System.Text.Encoding.UTF8.GetString(CsvWriter.ToBytes(table.Rows));

        csv.Should().Contain("01,  Doanh thu =x,1234.5,800");
        using var book = Open(XlsxWriter.ToBytes(table));
        book.Worksheet(1).LastRowUsed()!.RowNumber().Should().Be(table.Rows.Count);
    }

    private static FinancialStatementDto WithHeader(ReportHeaderDto? header)
    {
        var s = Statement();
        s.Header = header;
        s.Unit = "thousand VND";
        return s;
    }

    [Fact]
    public void The_company_block_and_the_signatories_are_printed_around_the_table()
    {
        var table = ReportTables.Statement(WithHeader(new ReportHeaderDto
        {
            CompanyName = "Công ty TNHH Mẫu", TaxCode = "0312345678", Address = "1 Lê Lợi", PreparedBy = "Nguyễn A", ChiefAccountant = "Trần B", Director = "Lê C",
        }));

        table.Rows[0].Should().Equal("Công ty TNHH Mẫu");
        table.Rows[1].Should().Equal("Tax code", "0312345678");
        table.Rows[2].Should().Equal("Address", "1 Lê Lợi");
        table.Rows[3][0].Should().Be("Income statement");
        table.Rows[4].Should().Contain("thousand VND");
        table.HeaderRow.Should().Be(6); // the column titles: code / item / amount / prior
        table.Rows[5].Should().Equal("Code", "Item", "Amount", "Prior");
        table.Rows[^1].Should().Equal("", "Nguyễn A", "Trần B", "Lê C");
        table.Rows[^2].Should().Equal("", "Prepared by", "Chief accountant", "Director");

        using var book = Open(XlsxWriter.ToBytes(table));
        var sheet = book.Worksheet(1);
        sheet.Cell(1, 1).GetString().Should().Be("Công ty TNHH Mẫu");
        sheet.Row(6).Style.Font.Bold.Should().BeTrue();
        sheet.SheetView.SplitRow.Should().Be(6);
        System.Text.Encoding.UTF8.GetString(CsvWriter.ToBytes(table.Rows)).Should().Contain("Tax code,0312345678").And.Contain(",Nguyễn A,Trần B,Lê C");
    }

    [Fact]
    public void Without_a_header_the_table_is_unchanged_and_only_filled_lines_are_printed()
    {
        var plain = ReportTables.Statement(WithHeader(null));
        plain.Rows[0][0].Should().Be("Income statement");
        plain.HeaderRow.Should().Be(3);
        plain.Rows.Should().NotContain(r => r.Count > 1 && (string?)r[1] == "Prepared by");

        var onlyName = ReportTables.Statement(WithHeader(new ReportHeaderDto { CompanyName = "ACME" }));
        onlyName.Rows[0].Should().Equal("ACME");
        onlyName.Rows[1][0].Should().Be("Income statement"); // no empty tax-code / address lines, no signatory block
        onlyName.Rows.Should().NotContain(r => r.Count > 1 && (string?)r[1] == "Prepared by");
    }
}

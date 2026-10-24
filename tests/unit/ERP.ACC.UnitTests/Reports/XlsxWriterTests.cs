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
}

using ClosedXML.Excel;

namespace ERP.ACC.Application.Reports
{
    /// <summary>
    /// Excel (.xlsx) for the accounting reports (ROADMAP P1-2d). Amounts are real numeric cells (no decimal-separator problem in a Vietnamese-locale Excel),
    /// text is always stored as text — never as a formula — and still gets the apostrophe guard of <see cref="CsvWriter"/> so a description starting with
    /// = + - @ cannot run when the cell is edited. The header row is bold and frozen.
    /// </summary>
    public static class XlsxWriter
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public static byte[] ToBytes(ReportTable table)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add(SheetName(table.Sheet));

            for (var r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                for (var c = 0; c < row.Count; c++)
                {
                    var cell = sheet.Cell(r + 1, c + 1);
                    switch (row[c])
                    {
                        case null:
                            break;
                        case decimal d:
                            cell.Value = d;
                            cell.Style.NumberFormat.Format = "#,##0.00;[Red]-#,##0.00";
                            break;
                        default:
                            cell.Value = XLCellValue.FromObject(CsvWriter.Escape(row[c]!.ToString(), quote: false));
                            break;
                    }
                }
            }

            if (table.HeaderRow > 0)
            {
                sheet.Row(table.HeaderRow).Style.Font.Bold = true;
                sheet.SheetView.FreezeRows(table.HeaderRow);
            }
            sheet.Columns().AdjustToContents(1, Math.Min(table.Rows.Count, 200), 8, 60);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // an Excel sheet name is at most 31 characters and cannot contain : \ / ? * [ ]
        private static string SheetName(string name)
        {
            var clean = new string(name.Where(ch => !":\\/?*[]".Contains(ch)).ToArray()).Trim();
            if (clean.Length == 0)
                clean = "Report";
            return clean.Length > 31 ? clean[..31] : clean;
        }
    }
}

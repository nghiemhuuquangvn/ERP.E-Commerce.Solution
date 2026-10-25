using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace ERP.ACC.Application.Reports
{
    /// <summary>
    /// PDF for the statements B01 / B02 / B03 (ROADMAP P1-2f), built from the same <see cref="FinancialStatementDto"/> as the CSV and Excel exports so the
    /// header, the unit, the signatories and every amount agree. A4 portrait: company block, centred title, period and unit line, the indicator table
    /// (header repeated on each page, totals bold, negatives in parentheses, Vietnamese number format), unmapped accounts and notes in small print, the
    /// signatories, and "Trang x/y" in the footer. Fonts are embedded (<see cref="PdfFonts"/>).
    /// </summary>
    public static class PdfWriter
    {
        public static byte[] ToBytes(FinancialStatementDto statement)
        {
            PdfFonts.Ensure();
            var renderer = new PdfDocumentRenderer { Document = Build(statement) };
            renderer.RenderDocument();
            using var stream = new MemoryStream();
            renderer.PdfDocument.Save(stream, false);
            return stream.ToArray();
        }

        /// <summary>1.234.567 / (1.234.567); a fraction (VND only) is shown with a comma and two decimals.</summary>
        public static string FormatAmount(decimal value)
        {
            var abs = Math.Abs(value);
            var text = abs % 1m == 0m
                ? abs.ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', '.')
                : abs.ToString("#,##0.00", CultureInfo.InvariantCulture).Replace(',', '\u0001').Replace('.', ',').Replace('\u0001', '.');
            return value < 0m ? "(" + text + ")" : text;
        }

        /// <summary>The notes B09-DN as a PDF: company block, title, period and unit, then every section with its text and its table, the signatories at the end.</summary>
        public static byte[] ToBytes(NotesDto notes)
        {
            PdfFonts.Ensure();
            var renderer = new PdfDocumentRenderer { Document = Build(notes) };
            renderer.RenderDocument();
            using var stream = new MemoryStream();
            renderer.PdfDocument.Save(stream, false);
            return stream.ToArray();
        }

        public static Document Build(NotesDto n)
        {
            var document = new Document();
            document.Info.Title = n.Title;
            var normal = document.Styles[StyleNames.Normal]!;
            normal.Font.Name = PdfFonts.Family;
            normal.Font.Size = 9;

            var section = document.AddSection();
            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.Orientation = Orientation.Portrait;
            section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1.5);
            section.PageSetup.TopMargin = Unit.FromCentimeter(1.8);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);

            var footer = section.Footers.Primary.AddParagraph();
            footer.Format.Alignment = ParagraphAlignment.Center;
            footer.Format.Font.Size = 8;
            footer.AddText("Trang ");
            footer.AddPageField();
            footer.AddText("/");
            footer.AddNumPagesField();

            var header = n.Header;
            if (header != null)
            {
                if (header.CompanyName.Length > 0)
                    Line(section, header.CompanyName, bold: true);
                if (header.TaxCode.Length > 0)
                    Line(section, "Mã số thuế: " + header.TaxCode);
                if (header.Address.Length > 0)
                    Line(section, "Địa chỉ: " + header.Address);
            }

            var title = section.AddParagraph(n.Title);
            title.Format.Alignment = ParagraphAlignment.Center;
            title.Format.Font.Size = 13;
            title.Format.Font.Bold = true;
            title.Format.SpaceBefore = Unit.FromPoint(12);

            if (n.IsDraft)
            {
                var draft = section.AddParagraph("Bản nháp — kế toán cần rà soát trước khi nộp");
                draft.Format.Alignment = ParagraphAlignment.Center;
                draft.Format.Font.Size = 8;
                draft.Format.Font.Italic = true;
            }

            string D(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            var period = section.AddParagraph($"Từ {D(n.FromDate)} đến {D(n.ToDate)} — Đơn vị tính: {UnitLabel(n.Unit)}");
            period.Format.Alignment = ParagraphAlignment.Center;
            period.Format.SpaceAfter = Unit.FromPoint(6);

            foreach (var s in n.Sections)
            {
                var heading = section.AddParagraph($"{s.Number}. {s.Title}");
                heading.Format.Font.Bold = true;
                heading.Format.SpaceBefore = Unit.FromPoint(10);
                heading.Format.KeepWithNext = true;

                if (!string.IsNullOrEmpty(s.Text))
                {
                    foreach (var line in s.Text.Split('\n'))
                        section.AddParagraph(line);
                }
                else if (s.Table == null && !string.IsNullOrEmpty(s.Hint))
                {
                    var hint = section.AddParagraph("(Chưa có nội dung — " + s.Hint + ")");
                    hint.Format.Font.Italic = true;
                    hint.Format.Font.Size = 8;
                }

                if (s.Table != null)
                    AddNoteTable(section, s.Table);
            }

            foreach (var note in n.Notes)
                Small(section, "Ghi chú: " + note);

            if (header != null && (header.PreparedBy + header.ChiefAccountant + header.Director).Length > 0)
            {
                var sign = section.AddTable();
                sign.Format.SpaceBefore = Unit.FromPoint(18);
                sign.Format.KeepTogether = true;
                for (var i = 0; i < 3; i++)
                    sign.AddColumn(Unit.FromCentimeter(5.8));
                var titles = sign.AddRow();
                titles.Format.Font.Bold = true;
                titles.KeepWith = 1;
                Cell(titles, 0, "Người lập biểu", ParagraphAlignment.Center);
                Cell(titles, 1, "Kế toán trưởng", ParagraphAlignment.Center);
                Cell(titles, 2, "Giám đốc", ParagraphAlignment.Center);
                var names = sign.AddRow();
                names.Height = Unit.FromCentimeter(2.2);
                names.VerticalAlignment = VerticalAlignment.Bottom;
                Cell(names, 0, header.PreparedBy, ParagraphAlignment.Center);
                Cell(names, 1, header.ChiefAccountant, ParagraphAlignment.Center);
                Cell(names, 2, header.Director, ParagraphAlignment.Center);
            }

            return document;
        }

        private static void AddNoteTable(Section section, NoteTableDto data)
        {
            var table = section.AddTable();
            table.Borders.Width = 0.4;
            table.Format.Font.Size = 8;
            var numeric = data.Columns.Count;
            var labelWidth = numeric <= 1 ? 11.5 : numeric == 2 ? 8.5 : 6.5;
            table.AddColumn(Unit.FromCentimeter(labelWidth));
            var numberWidth = (17.5 - labelWidth) / Math.Max(1, numeric);
            for (var i = 0; i < numeric; i++)
                table.AddColumn(Unit.FromCentimeter(numberWidth));

            var head = table.AddRow();
            head.HeadingFormat = true;
            head.Format.Font.Bold = true;
            head.Shading.Color = Colors.LightGray;
            Cell(head, 0, "Chỉ tiêu", ParagraphAlignment.Left);
            for (var i = 0; i < numeric; i++)
                Cell(head, i + 1, data.Columns[i], ParagraphAlignment.Right);

            foreach (var r in data.Rows)
            {
                var row = table.AddRow();
                row.Format.Font.Bold = r.IsTotal;
                Cell(row, 0, r.Label, ParagraphAlignment.Left);
                for (var i = 0; i < numeric; i++)
                    Cell(row, i + 1, i < r.Cells.Count ? FormatAmount(r.Cells[i]) : "", ParagraphAlignment.Right);
            }
        }

        private static string UnitLabel(string unit) => unit switch
        {
            "thousand VND" => "nghìn đồng",
            "million VND" => "triệu đồng",
            _ => "đồng",
        };

        /// <summary>The document model (public so tests can read the text without parsing the PDF).</summary>
        public static Document Build(FinancialStatementDto s)
        {
            var document = new Document();
            document.Info.Title = s.Title;

            var normal = document.Styles[StyleNames.Normal]!;
            normal.Font.Name = PdfFonts.Family;
            normal.Font.Size = 9;

            var section = document.AddSection();
            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.Orientation = Orientation.Portrait;
            section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1.5);
            section.PageSetup.TopMargin = Unit.FromCentimeter(1.8);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);

            var footer = section.Footers.Primary.AddParagraph();
            footer.Format.Alignment = ParagraphAlignment.Center;
            footer.Format.Font.Size = 8;
            footer.AddText("Trang ");
            footer.AddPageField();
            footer.AddText("/");
            footer.AddNumPagesField();

            var header = s.Header;
            if (header != null)
            {
                if (header.CompanyName.Length > 0)
                    Line(section, header.CompanyName, bold: true);
                if (header.TaxCode.Length > 0)
                    Line(section, "Mã số thuế: " + header.TaxCode);
                if (header.Address.Length > 0)
                    Line(section, "Địa chỉ: " + header.Address);
            }

            var title = section.AddParagraph(s.Title);
            title.Format.Alignment = ParagraphAlignment.Center;
            title.Format.Font.Size = 13;
            title.Format.Font.Bold = true;
            title.Format.SpaceBefore = Unit.FromPoint(12);

            if (s.IsDraft)
            {
                var draft = section.AddParagraph("Bản nháp — kế toán cần rà soát trước khi nộp");
                draft.Format.Alignment = ParagraphAlignment.Center;
                draft.Format.Font.Size = 8;
                draft.Format.Font.Italic = true;
            }

            var period = section.AddParagraph(PeriodText(s));
            period.Format.Alignment = ParagraphAlignment.Center;
            period.Format.SpaceAfter = Unit.FromPoint(8);

            var compare = s.Rows.Any(r => r.PriorAmount.HasValue);
            var table = section.AddTable();
            table.Borders.Width = 0.4;
            table.Rows.LeftIndent = 0;
            table.AddColumn(Unit.FromCentimeter(1.5));
            table.AddColumn(Unit.FromCentimeter(compare ? 8 : 10.5));
            table.AddColumn(Unit.FromCentimeter(compare ? 3.5 : 5));
            if (compare)
                table.AddColumn(Unit.FromCentimeter(3.5));

            var head = table.AddRow();
            head.HeadingFormat = true;
            head.Format.Font.Bold = true;
            head.Shading.Color = Colors.LightGray;
            Cell(head, 0, "Mã số", ParagraphAlignment.Center);
            Cell(head, 1, "Chỉ tiêu", ParagraphAlignment.Left);
            Cell(head, 2, "Số cuối kỳ", ParagraphAlignment.Right);
            if (compare)
                Cell(head, 3, "Số kỳ trước", ParagraphAlignment.Right);

            foreach (var row in s.Rows)
            {
                var r = table.AddRow();
                r.Format.Font.Bold = row.IsTotal;
                Cell(r, 0, row.Code, ParagraphAlignment.Center);
                Cell(r, 1, new string(' ', Math.Max(0, row.Indent - 1) * 3) + row.Name, ParagraphAlignment.Left);
                Cell(r, 2, FormatAmount(row.Amount), ParagraphAlignment.Right);
                if (compare)
                    Cell(r, 3, row.PriorAmount.HasValue ? FormatAmount(row.PriorAmount.Value) : "", ParagraphAlignment.Right);
            }

            if (s.IsBalanced.HasValue)
                Small(section, s.IsBalanced.Value ? "Kiểm tra: cân đối." : "Kiểm tra: KHÔNG cân đối.");
            foreach (var u in s.Unmapped)
                Small(section, $"Chưa ánh xạ: {u.AccountNumber} {u.AccountName}: {FormatAmount(u.Balance)} đồng");
            foreach (var note in s.Notes)
                Small(section, "Ghi chú: " + note);

            if (header != null && (header.PreparedBy + header.ChiefAccountant + header.Director).Length > 0)
            {
                var sign = section.AddTable();
                sign.Format.SpaceBefore = Unit.FromPoint(18);
                sign.Format.KeepTogether = true;
                for (var i = 0; i < 3; i++)
                    sign.AddColumn(Unit.FromCentimeter(5.8));
                var titles = sign.AddRow();
                titles.Format.Font.Bold = true;
                titles.KeepWith = 1;
                Cell(titles, 0, "Người lập biểu", ParagraphAlignment.Center);
                Cell(titles, 1, "Kế toán trưởng", ParagraphAlignment.Center);
                Cell(titles, 2, "Giám đốc", ParagraphAlignment.Center);
                var names = sign.AddRow();
                names.Height = Unit.FromCentimeter(2.2);
                names.VerticalAlignment = VerticalAlignment.Bottom;
                Cell(names, 0, header.PreparedBy, ParagraphAlignment.Center);
                Cell(names, 1, header.ChiefAccountant, ParagraphAlignment.Center);
                Cell(names, 2, header.Director, ParagraphAlignment.Center);
            }

            return document;
        }

        private static string PeriodText(FinancialStatementDto s)
        {
            string D(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            var range = s.FromDate.HasValue && s.ToDate.HasValue ? $"Từ {D(s.FromDate.Value)} đến {D(s.ToDate.Value)}"
                : s.ToDate.HasValue ? $"Tại ngày {D(s.ToDate.Value)}" : string.Empty;
            return $"{range} — Đơn vị tính: {UnitLabel(s.Unit)}".TrimStart(' ', '—');
        }

        private static void Line(Section section, string text, bool bold = false)
        {
            var p = section.AddParagraph(text);
            p.Format.Font.Bold = bold;
        }

        private static void Small(Section section, string text)
        {
            var p = section.AddParagraph(text);
            p.Format.Font.Size = 7.5;
            p.Format.SpaceBefore = Unit.FromPoint(3);
        }

        private static void Cell(Row row, int index, string text, ParagraphAlignment alignment)
        {
            var cell = row.Cells[index];
            cell.Format.Alignment = alignment;
            cell.AddParagraph(text);
        }
    }
}

using System.Globalization;
using System.Text;
using ERP.ACC.Application.DTOs;

namespace ERP.ACC.Application.Reports
{
    /// <summary>
    /// CSV for the accounting reports (ROADMAP P1-2c): UTF-8 with a byte-order mark so Excel reads Vietnamese correctly, CRLF line breaks, RFC 4180 quoting,
    /// numbers in invariant form (a dot as the decimal separator, no thousands separator) and text cells that start with = + - @ neutralised against
    /// spreadsheet formula injection (an account or entry description is user text). The report shapes live in <see cref="ReportTables"/>.
    /// </summary>
    public static class CsvWriter
    {
        /// <summary>Cells are strings, decimals (written in invariant form) or null.</summary>
        public static byte[] ToBytes(IEnumerable<IReadOnlyList<object?>> rows)
        {
            var sb = new StringBuilder();
            foreach (var row in rows)
                sb.Append(string.Join(",", row.Select(Cell))).Append("\r\n");
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }

        private static string Cell(object? value) => value switch
        {
            null => string.Empty,
            decimal d => Num(d),
            _ => Escape(value.ToString()),
        };

        public static string Escape(string? value) => Escape(value, quote: true);

        /// <summary>The injection guard on its own (<paramref name="quote"/> false = no CSV quoting), shared with the Excel export.</summary>
        public static string Escape(string? value, bool quote)
        {
            var text = value ?? string.Empty;
            if (text.Length > 0 && text[0] is '=' or '+' or '@' || (text.Length > 1 && text[0] == '-' && !decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
                text = "'" + text;
            if (!quote)
                return text;
            return text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
        }

        public static string Num(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        public static string Num(decimal? value) => value.HasValue ? Num(value.Value) : string.Empty;

        // ---- report shapes --------------------------------------------------------------------------------------------

        public static byte[] FromStatement(FinancialStatementDto s) => ToBytes(ReportTables.Statement(s).Rows);

        public static byte[] FromTrialBalance(TrialBalanceDto t) => ToBytes(ReportTables.TrialBalance(t).Rows);

        public static byte[] FromLedger(AccountLedgerDto l) => ToBytes(ReportTables.Ledger(l).Rows);

        public static byte[] FromCashFlow(CashFlowStatementDto c) => ToBytes(ReportTables.CashFlow(c).Rows);
    }
}

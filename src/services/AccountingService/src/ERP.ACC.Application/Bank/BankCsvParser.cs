using System.Globalization;
using System.Text;

namespace ERP.ACC.Application.Bank
{
    /// <summary>One row read from a bank's CSV export. <see cref="Amount"/> is signed: a deposit positive, a withdrawal negative.</summary>
    public record ParsedStatementRow(int RowNumber, DateTime Date, string? Description, string? Reference, decimal Amount, decimal? Balance);

    public record StatementRowError(int RowNumber, string Message);

    public record BankCsvResult(List<ParsedStatementRow> Rows, List<StatementRowError> Errors);

    /// <summary>
    /// Reads the CSV a bank exports (ROADMAP P1-4). Columns are found by their header name (English or Vietnamese), in any order, with a comma, semicolon or tab between them:
    /// a date, an optional description and reference, and either one signed amount or a deposit (credit) and a withdrawal (debit) column, and an optional balance.
    /// </summary>
    public static class BankCsvParser
    {
        private static readonly string[] DateNames = { "date", "transaction date", "posting date", "booking date", "value date", "ngay", "ngay giao dich", "ngay gd" };
        private static readonly string[] DescriptionNames = { "description", "details", "narrative", "memo", "particulars", "noi dung", "dien giai", "mo ta" };
        private static readonly string[] ReferenceNames = { "reference", "ref", "ref no", "reference number", "transaction id", "so tham chieu", "ma giao dich", "so chung tu" };
        private static readonly string[] AmountNames = { "amount", "so tien", "gia tri" };
        private static readonly string[] DepositNames = { "deposit", "deposits", "credit", "credits", "money in", "paid in", "co", "ghi co", "tien vao", "so tien co" };
        private static readonly string[] WithdrawalNames = { "withdrawal", "withdrawals", "debit", "debits", "money out", "paid out", "no", "ghi no", "tien ra", "so tien no" };
        private static readonly string[] BalanceNames = { "balance", "running balance", "so du" };

        private static readonly string[] DateFormats =
        {
            "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss", "dd/MM/yyyy HH:mm:ss",
        };

        public static BankCsvResult Parse(string? csv)
        {
            var rows = new List<ParsedStatementRow>();
            var errors = new List<StatementRowError>();
            if (string.IsNullOrWhiteSpace(csv))
            {
                errors.Add(new StatementRowError(0, "The CSV is empty."));
                return new BankCsvResult(rows, errors);
            }

            var lines = csv.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select((text, i) => (Text: text, Number: i + 1)).Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();
            var delimiter = DetectDelimiter(lines[0].Text);
            var header = Split(lines[0].Text, delimiter).Select(Normalise).ToList();

            int Find(string[] names) => header.FindIndex(h => names.Contains(h));
            var date = Find(DateNames);
            var description = Find(DescriptionNames);
            var reference = Find(ReferenceNames);
            var amount = Find(AmountNames);
            var deposit = Find(DepositNames);
            var withdrawal = Find(WithdrawalNames);
            var balance = Find(BalanceNames);

            if (date < 0)
                errors.Add(new StatementRowError(lines[0].Number, "The header has no date column (date / ngày)."));
            if (amount < 0 && deposit < 0 && withdrawal < 0)
                errors.Add(new StatementRowError(lines[0].Number, "The header has no amount column: give an amount, or a deposit and a withdrawal column."));
            if (errors.Count > 0)
                return new BankCsvResult(rows, errors);

            foreach (var (text, number) in lines.Skip(1))
            {
                var cells = Split(text, delimiter);
                string? Cell(int index) => index >= 0 && index < cells.Count ? cells[index].Trim() : null;

                if (!TryDate(Cell(date), out var day))
                {
                    errors.Add(new StatementRowError(number, $"'{Cell(date)}' is not a date (use yyyy-MM-dd or dd/MM/yyyy)."));
                    continue;
                }

                decimal value;
                if (amount >= 0)
                {
                    if (!TryAmount(Cell(amount), out value))
                    {
                        errors.Add(new StatementRowError(number, $"'{Cell(amount)}' is not an amount."));
                        continue;
                    }
                }
                else
                {
                    var dep = Cell(deposit);
                    var wd = Cell(withdrawal);
                    var hasDep = !string.IsNullOrWhiteSpace(dep);
                    var hasWd = !string.IsNullOrWhiteSpace(wd);
                    decimal d = 0m, w = 0m;
                    if ((hasDep && !TryAmount(dep, out d)) || (hasWd && !TryAmount(wd, out w)))
                    {
                        errors.Add(new StatementRowError(number, "The deposit / withdrawal is not an amount."));
                        continue;
                    }
                    if (hasDep && hasWd && Math.Abs(d) > 0m && Math.Abs(w) > 0m)
                    {
                        errors.Add(new StatementRowError(number, "A line has both a deposit and a withdrawal."));
                        continue;
                    }
                    value = Math.Abs(d) - Math.Abs(w);
                }

                if (value == 0m)
                {
                    errors.Add(new StatementRowError(number, "The line moves no money."));
                    continue;
                }

                decimal? running = null;
                if (balance >= 0 && !string.IsNullOrWhiteSpace(Cell(balance)))
                {
                    if (!TryAmount(Cell(balance), out var b))
                    {
                        errors.Add(new StatementRowError(number, $"'{Cell(balance)}' is not a balance."));
                        continue;
                    }
                    running = b;
                }

                rows.Add(new ParsedStatementRow(number, day, Cell(description), Cell(reference), Math.Round(value, 2), running));
            }

            if (rows.Count == 0 && errors.Count == 0)
                errors.Add(new StatementRowError(0, "The CSV has a header and no lines."));
            return new BankCsvResult(rows, errors);
        }

        private static char DetectDelimiter(string header)
        {
            var best = ',';
            var most = -1;
            foreach (var c in new[] { ',', ';', '\t' })
            {
                var count = header.Count(ch => ch == c);
                if (count > most) { best = c; most = count; }
            }
            return best;
        }

        /// <summary>Splits a line on the delimiter, honouring "quoted, fields" and doubled quotes.</summary>
        private static List<string> Split(string line, char delimiter)
        {
            var cells = new List<string>();
            var current = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else current.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == delimiter) { cells.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
            cells.Add(current.ToString());
            return cells;
        }

        /// <summary>A header name without case, accents and surrounding spaces ("Ngày GD" → "ngay gd").</summary>
        private static string Normalise(string header)
        {
            var decomposed = header.Trim().Trim('﻿').ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC).Trim();
        }

        private static bool TryDate(string? text, out DateTime day)
        {
            day = default;
            return !string.IsNullOrWhiteSpace(text)
                && DateTime.TryParseExact(text.Trim().Trim('﻿'), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out day)
                && (day = day.Date) == day;
        }

        /// <summary>1,500,000 / 1.500.000 / 1 500 000 / 1,234.56 / 1.234,56 / (500) / -500; the last separator is the decimal one when both appear.</summary>
        public static bool TryAmount(string? text, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var s = text.Trim().Replace(" ", "").Replace(" ", "");
            var negative = false;
            if (s.StartsWith('(') && s.EndsWith(')')) { negative = true; s = s[1..^1]; }
            if (s.StartsWith('-')) { negative = !negative; s = s[1..]; }
            else if (s.StartsWith('+')) s = s[1..];
            s = s.TrimEnd('-', 'D', 'C', 'd', 'c');
            if (s.Length == 0 || !s.All(ch => char.IsDigit(ch) || ch == ',' || ch == '.'))
                return false;

            var lastComma = s.LastIndexOf(',');
            var lastDot = s.LastIndexOf('.');
            char? decimalSeparator = null;
            if (lastComma >= 0 && lastDot >= 0)
                decimalSeparator = lastComma > lastDot ? ',' : '.';
            else if (lastComma >= 0 || lastDot >= 0)
            {
                var sep = lastComma >= 0 ? ',' : '.';
                var count = s.Count(ch => ch == sep);
                var digitsAfter = s.Length - s.LastIndexOf(sep) - 1;
                // one separator with one or two digits after it is a decimal point; groups of three are thousands
                if (count == 1 && digitsAfter != 3)
                    decimalSeparator = sep;
            }

            var normalised = new StringBuilder();
            foreach (var ch in s)
            {
                if (char.IsDigit(ch)) normalised.Append(ch);
                else if (ch == decimalSeparator) normalised.Append('.');
            }
            if (!decimal.TryParse(normalised.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                return false;
            if (negative) value = -value;
            return true;
        }
    }
}

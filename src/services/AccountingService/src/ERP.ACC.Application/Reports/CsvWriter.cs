using System.Globalization;
using System.Text;
using ERP.ACC.Application.DTOs;

namespace ERP.ACC.Application.Reports
{
    /// <summary>
    /// CSV for the accounting reports (ROADMAP P1-2c): UTF-8 with a byte-order mark so Excel reads Vietnamese correctly, CRLF line breaks, RFC 4180 quoting,
    /// numbers in invariant form (a dot as the decimal separator, no thousands separator) and text cells that start with = + - @ neutralised against
    /// spreadsheet formula injection (an account or entry description is user text).
    /// </summary>
    public static class CsvWriter
    {
        public static byte[] ToBytes(IEnumerable<IReadOnlyList<string>> rows)
        {
            var sb = new StringBuilder();
            foreach (var row in rows)
                sb.Append(string.Join(",", row.Select(Escape))).Append("\r\n");
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }

        public static string Escape(string? value)
        {
            var text = value ?? string.Empty;
            if (text.Length > 0 && text[0] is '=' or '+' or '@' || (text.Length > 1 && text[0] == '-' && !decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
                text = "'" + text;
            return text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
        }

        public static string Num(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        public static string Num(decimal? value) => value.HasValue ? Num(value.Value) : string.Empty;

        // ---- report shapes --------------------------------------------------------------------------------------------

        public static byte[] FromStatement(FinancialStatementDto s)
        {
            var compare = s.Rows.Any(r => r.PriorAmount.HasValue);
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { s.Title },
                new[] { "Period", s.FromDate?.ToString("yyyy-MM-dd") ?? "", s.ToDate?.ToString("yyyy-MM-dd") ?? "" },
                compare ? new[] { "Code", "Item", "Amount", "Prior" } : new[] { "Code", "Item", "Amount" },
            };
            rows.AddRange(s.Rows.Select(r => compare
                ? (IReadOnlyList<string>)new[] { r.Code, new string(' ', r.Indent * 2) + r.Name, Num(r.Amount), Num(r.PriorAmount) }
                : new[] { r.Code, new string(' ', r.Indent * 2) + r.Name, Num(r.Amount) }));
            if (s.IsBalanced.HasValue)
                rows.Add(new[] { "", "Balanced", s.IsBalanced.Value ? "yes" : "NO" });
            foreach (var u in s.Unmapped)
                rows.Add(new[] { "unmapped", u.AccountNumber + " " + u.AccountName, Num(u.Balance) });
            foreach (var note in s.Notes)
                rows.Add(new[] { "note", note });
            return ToBytes(rows);
        }

        public static byte[] FromTrialBalance(TrialBalanceDto t)
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "Account", "Name", "Opening debit", "Opening credit", "Period debit", "Period credit", "Closing debit", "Closing credit" },
            };
            rows.AddRange(t.Lines.Select(l => (IReadOnlyList<string>)new[]
            {
                l.AccountNumber, new string(' ', (l.Level - 1) * 2) + l.AccountName,
                Num(l.OpeningDebit), Num(l.OpeningCredit), Num(l.PeriodDebit), Num(l.PeriodCredit), Num(l.ClosingDebit), Num(l.ClosingCredit),
            }));
            rows.Add(new[]
            {
                "Total", t.Totals.IsBalanced ? "balanced" : "NOT balanced",
                Num(t.Totals.OpeningDebit), Num(t.Totals.OpeningCredit), Num(t.Totals.PeriodDebit), Num(t.Totals.PeriodCredit),
                Num(t.Totals.ClosingDebit), Num(t.Totals.ClosingCredit),
            });
            return ToBytes(rows);
        }

        public static byte[] FromLedger(AccountLedgerDto l)
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "Account ledger", l.AccountNumber, l.AccountName },
                new[] { "Opening balance", Num(l.OpeningBalance) },
                new[] { "Date", "Entry", "Description", "Account", "Counter accounts", "Debit", "Credit", "Balance", "Status" },
            };
            rows.AddRange(l.Lines.Select(x => (IReadOnlyList<string>)new[]
            {
                x.EntryDate.ToString("yyyy-MM-dd"), x.EntryNumber, x.Description, x.AccountNumber, x.CounterAccounts,
                Num(x.Debit), Num(x.Credit), Num(x.Balance), x.IsClosingEntry ? x.Status + " (closing)" : x.Status,
            }));
            rows.Add(new[] { "Total", "", "", "", "", Num(l.TotalDebit), Num(l.TotalCredit) });
            rows.Add(new[] { "Closing balance", Num(l.ClosingBalance) });
            return ToBytes(rows);
        }

        public static byte[] FromCashFlow(CashFlowStatementDto c)
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "Cash flow (direct method)", c.FromDate.ToString("yyyy-MM-dd"), c.ToDate.ToString("yyyy-MM-dd") },
                new[] { "Opening cash", Num(c.OpeningCash) },
                new[] { "Activity", "Account", "Name", "Received", "Paid", "Net" },
            };
            foreach (var section in new[] { c.Operating, c.Investing, c.Financing })
            {
                rows.AddRange(section.Lines.Select(l => (IReadOnlyList<string>)new[]
                    { section.Category, l.AccountNumber, l.AccountName, Num(l.Inflow), Num(l.Outflow), Num(l.Net) }));
                rows.Add(new[] { section.Category + " total", "", "", Num(section.TotalInflow), Num(section.TotalOutflow), Num(section.Net) });
            }
            rows.Add(new[] { "Net change", Num(c.NetChange) });
            rows.Add(new[] { "Closing cash", Num(c.ClosingCash) });
            rows.Add(new[] { "Reconciled", c.IsReconciled ? "yes" : "NO (difference " + Num(c.Difference) + ")" });
            return ToBytes(rows);
        }
    }
}

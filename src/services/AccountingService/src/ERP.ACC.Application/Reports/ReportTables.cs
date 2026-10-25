using ERP.ACC.Application.DTOs;

namespace ERP.ACC.Application.Reports
{
    /// <summary>A report as rows of cells (string, decimal or null) that both exports serialize; <see cref="HeaderRow"/> is 1-based (0 = none).</summary>
    public sealed record ReportTable(string Sheet, IReadOnlyList<IReadOnlyList<object?>> Rows, int HeaderRow);

    /// <summary>The report shapes shared by the CSV and Excel exports (ROADMAP P1-2c / P1-2d), so a column added once shows in both.</summary>
    public static class ReportTables
    {
        private static string Date(DateTime d) => d.ToString("yyyy-MM-dd");

        public static ReportTable Statement(FinancialStatementDto s)
        {
            var compare = s.Rows.Any(r => r.PriorAmount.HasValue);
            var rows = new List<IReadOnlyList<object?>>();

            // the company block above the title, as on the filed forms (only the lines the tenant filled in)
            if (s.Header != null)
            {
                if (s.Header.CompanyName.Length > 0)
                    rows.Add(new object?[] { s.Header.CompanyName });
                if (s.Header.TaxCode.Length > 0)
                    rows.Add(new object?[] { "Tax code", s.Header.TaxCode });
                if (s.Header.Address.Length > 0)
                    rows.Add(new object?[] { "Address", s.Header.Address });
            }

            rows.Add(new object?[] { s.Title });
            rows.Add(new object?[] { "Period", s.FromDate.HasValue ? Date(s.FromDate.Value) : "", s.ToDate.HasValue ? Date(s.ToDate.Value) : "", "Unit", s.Unit });
            rows.Add(compare ? new object?[] { "Code", "Item", "Amount", "Prior" } : new object?[] { "Code", "Item", "Amount" });
            var headerRow = rows.Count;

            rows.AddRange(s.Rows.Select(r => compare
                ? new object?[] { r.Code, new string(' ', r.Indent * 2) + r.Name, r.Amount, r.PriorAmount }
                : new object?[] { r.Code, new string(' ', r.Indent * 2) + r.Name, r.Amount }));
            if (s.IsBalanced.HasValue)
                rows.Add(new object?[] { "", "Balanced", s.IsBalanced.Value ? "yes" : "NO" });
            foreach (var u in s.Unmapped)
                rows.Add(new object?[] { "unmapped", u.AccountNumber + " " + u.AccountName, u.Balance });
            foreach (var note in s.Notes)
                rows.Add(new object?[] { "note", note });

            // the signatories under the table
            if (s.Header != null && (s.Header.PreparedBy + s.Header.ChiefAccountant + s.Header.Director).Length > 0)
            {
                rows.Add(new object?[] { "" });
                rows.Add(new object?[] { "", "Prepared by", "Chief accountant", "Director" });
                rows.Add(new object?[] { "", s.Header.PreparedBy, s.Header.ChiefAccountant, s.Header.Director });
            }

            return new ReportTable(s.LayoutId, rows, headerRow);
        }

        /// <summary>The notes B09: company block, then each section as a numbered title, its text lines and its table.</summary>
        public static ReportTable Notes(NotesDto n)
        {
            var rows = new List<IReadOnlyList<object?>>();
            if (n.Header != null)
            {
                if (n.Header.CompanyName.Length > 0)
                    rows.Add(new object?[] { n.Header.CompanyName });
                if (n.Header.TaxCode.Length > 0)
                    rows.Add(new object?[] { "Tax code", n.Header.TaxCode });
                if (n.Header.Address.Length > 0)
                    rows.Add(new object?[] { "Address", n.Header.Address });
            }

            rows.Add(new object?[] { n.Title });
            rows.Add(new object?[] { "Period", Date(n.FromDate), Date(n.ToDate), "Unit", n.Unit });
            foreach (var s in n.Sections)
            {
                rows.Add(new object?[] { "" });
                rows.Add(new object?[] { s.Number, s.Title });
                if (!string.IsNullOrEmpty(s.Text))
                    foreach (var line in s.Text.Split('\n'))
                        rows.Add(new object?[] { "", line });
                else if (s.Table == null && !string.IsNullOrEmpty(s.Hint))
                    rows.Add(new object?[] { "", "(" + s.Hint + ")" });

                if (s.Table != null)
                {
                    rows.Add(new object?[] { "", "" }.Concat(s.Table.Columns.Cast<object?>()).ToArray());
                    foreach (var r in s.Table.Rows)
                        rows.Add(new object?[] { "", r.Label }.Concat(r.Cells.Select(c => (object?)c)).ToArray());
                }
            }

            foreach (var note in n.Notes)
                rows.Add(new object?[] { "note", note });

            if (n.Header != null && (n.Header.PreparedBy + n.Header.ChiefAccountant + n.Header.Director).Length > 0)
            {
                rows.Add(new object?[] { "" });
                rows.Add(new object?[] { "", "Prepared by", "Chief accountant", "Director" });
                rows.Add(new object?[] { "", n.Header.PreparedBy, n.Header.ChiefAccountant, n.Header.Director });
            }

            return new ReportTable("B09-DN", rows, 0);
        }

        public static ReportTable LoanReconciliation(LoanReconciliationDto r)
        {
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "Loan ledger vs books", Date(r.AsOfDate), r.AccountNumber, r.AccountName },
                new object?[] { "Loan", "Lender", "Posted", "Outstanding" },
            };
            rows.AddRange(r.Loans.Select(l => (IReadOnlyList<object?>)new object?[] { l.LoanNumber, l.Lender, l.PostedToLedger ? "yes" : "no", l.Outstanding }));
            rows.Add(new object?[] { "Ledger total", "", "", r.LedgerTotal });
            rows.Add(new object?[] { "Book balance", "", "", r.BookBalance });
            rows.Add(new object?[] { "Difference", "", r.IsReconciled ? "reconciled" : "NOT reconciled", r.Difference });
            foreach (var note in r.Notes)
                rows.Add(new object?[] { "note", note });
            return new ReportTable("Loans", rows, 3);
        }

        public static ReportTable OffBalance(ERP.ACC.Application.Handlers.OffBalanceReportDto r)
        {
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "Off-balance register", Date(r.FromDate), Date(r.ToDate) },
                new object?[] { "Account", "Name", "Opening", "In", "Out", "Closing" },
            };
            rows.AddRange(r.Lines.Select(l => (IReadOnlyList<object?>)new object?[] { l.AccountNumber, l.AccountName, l.Opening, l.In, l.Out, l.Closing }));
            foreach (var note in r.Notes)
                rows.Add(new object?[] { "note", note });
            return new ReportTable("Off-balance", rows, 2);
        }

        public static ReportTable TrialBalance(TrialBalanceDto t)
        {
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "Account", "Name", "Opening debit", "Opening credit", "Period debit", "Period credit", "Closing debit", "Closing credit" },
            };
            rows.AddRange(t.Lines.Select(l => (IReadOnlyList<object?>)new object?[]
            {
                l.AccountNumber, new string(' ', (l.Level - 1) * 2) + l.AccountName,
                l.OpeningDebit, l.OpeningCredit, l.PeriodDebit, l.PeriodCredit, l.ClosingDebit, l.ClosingCredit,
            }));
            rows.Add(new object?[]
            {
                "Total", t.Totals.IsBalanced ? "balanced" : "NOT balanced",
                t.Totals.OpeningDebit, t.Totals.OpeningCredit, t.Totals.PeriodDebit, t.Totals.PeriodCredit, t.Totals.ClosingDebit, t.Totals.ClosingCredit,
            });
            return new ReportTable("Trial balance", rows, 1);
        }

        public static ReportTable Ledger(AccountLedgerDto l)
        {
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "Account ledger", l.AccountNumber, l.AccountName },
                new object?[] { "Opening balance", l.OpeningBalance },
                new object?[] { "Date", "Entry", "Description", "Account", "Counter accounts", "Debit", "Credit", "Balance", "Status" },
            };
            rows.AddRange(l.Lines.Select(x => (IReadOnlyList<object?>)new object?[]
            {
                Date(x.EntryDate), x.EntryNumber, x.Description, x.AccountNumber, x.CounterAccounts,
                x.Debit, x.Credit, x.Balance, x.IsClosingEntry ? x.Status + " (closing)" : x.Status,
            }));
            rows.Add(new object?[] { "Total", "", "", "", "", l.TotalDebit, l.TotalCredit });
            rows.Add(new object?[] { "Closing balance", l.ClosingBalance });
            return new ReportTable("Account ledger", rows, 3);
        }

        public static ReportTable CashFlow(CashFlowStatementDto c)
        {
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "Cash flow (direct method)", Date(c.FromDate), Date(c.ToDate) },
                new object?[] { "Opening cash", c.OpeningCash },
                new object?[] { "Activity", "Account", "Name", "Received", "Paid", "Net" },
            };
            foreach (var section in new[] { c.Operating, c.Investing, c.Financing })
            {
                rows.AddRange(section.Lines.Select(l => (IReadOnlyList<object?>)new object?[]
                    { section.Category, l.AccountNumber, l.AccountName, l.Inflow, l.Outflow, l.Net }));
                rows.Add(new object?[] { section.Category + " total", "", "", section.TotalInflow, section.TotalOutflow, section.Net });
            }
            rows.Add(new object?[] { "Net change", c.NetChange });
            rows.Add(new object?[] { "Closing cash", c.ClosingCash });
            rows.Add(new object?[] { "Reconciled", c.IsReconciled ? "yes" : "NO (difference " + CsvWriter.Num(c.Difference) + ")" });
            return new ReportTable("Cash flow", rows, 3);
        }
    }
}

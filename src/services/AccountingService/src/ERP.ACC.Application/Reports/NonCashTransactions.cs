using ERP.ACC.Application.DTOs;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;

namespace ERP.ACC.Application.Reports
{
    /// <summary>
    /// The non-cash transactions of the notes (section VII, ROADMAP P1-2j), found in the journal: an entry that touches no cash account and either buys fixed assets on a
    /// liability (assets acquired on credit or by borrowing) or moves a liability into owners' equity (debt converted into equity). The entries are classified by their
    /// accounts only, so the table is a starting point for the accountant, who confirms it in the text of the section. Closing entries are left out.
    /// </summary>
    public static class NonCashTransactions
    {
        public static NoteTableDto Build(
            IReadOnlyCollection<Account> accounts, IEnumerable<LedgerRow> lines, NotePrefixes prefixes, NoteStrings strings, decimal divisor)
        {
            var byId = accounts.ToDictionary(a => a.Id);
            var assetPrefixes = prefixes.FixedCost.Concat(prefixes.ConstructionInProgress).ToArray();
            // retained earnings are not a conversion target: debt becomes capital, not profit
            var retained = new[] { "421", "3100" };

            bool Starts(Account a, IEnumerable<string> p) => p.Any(x => a.AccountNumber.StartsWith(x, StringComparison.Ordinal));

            decimal assetsOnCredit = 0m, debtToEquity = 0m;
            foreach (var entry in lines.Where(l => l.ReferenceType != JournalEntry.PeriodClosingType).GroupBy(l => l.EntryId))
            {
                var rows = entry.Where(l => byId.ContainsKey(l.AccountId)).Select(l => (Line: l, Account: byId[l.AccountId])).ToList();
                if (rows.Count == 0 || rows.Any(r => r.Account.IsCash))
                    continue;

                var liabilityCredit = rows.Where(r => r.Account.AccountType == (int)AccountType.Liability).Sum(r => r.Line.Credit);
                var liabilityDebit = rows.Where(r => r.Account.AccountType == (int)AccountType.Liability).Sum(r => r.Line.Debit);
                var assetDebit = rows.Where(r => Starts(r.Account, assetPrefixes)).Sum(r => r.Line.Debit);
                var equityCredit = rows.Where(r => r.Account.AccountType == (int)AccountType.Equity && !Starts(r.Account, retained)).Sum(r => r.Line.Credit);

                assetsOnCredit += Math.Min(assetDebit, liabilityCredit);
                debtToEquity += Math.Min(liabilityDebit, equityCredit);
            }

            var table = new NoteTableDto { Columns = new() { strings.Amount } };
            if (assetsOnCredit != 0m)
                table.Rows.Add(new NoteTableRowDto { Label = strings.NonCashAssetsOnCredit, Cells = new() { StatementEngine.RoundTo(assetsOnCredit, divisor) } });
            if (debtToEquity != 0m)
                table.Rows.Add(new NoteTableRowDto { Label = strings.NonCashDebtToEquity, Cells = new() { StatementEngine.RoundTo(debtToEquity, divisor) } });
            if (table.Rows.Count > 0)
                table.Rows.Add(new NoteTableRowDto { Label = strings.Total, IsTotal = true, Cells = new() { table.Rows.Sum(r => r.Cells[0]) } });
            return table;
        }
    }
}

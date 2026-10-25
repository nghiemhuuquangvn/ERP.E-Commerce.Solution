using ERP.ACC.Application.DTOs;
using ERP.ACC.Domain.Entities;

namespace ERP.ACC.Application.Reports
{
    /// <summary>The maturity split of one balance-sheet date, tied to the ledger (shared by B01 and the notes B09).</summary>
    public static class MaturityLedger
    {
        public static StatementMaturity For(
            DateTime asOf, IReadOnlyCollection<Account> accounts, IReadOnlyDictionary<Guid, AccountBalanceSummary> balances,
            List<Invoice> invoices, List<Bill> bills, List<Loan> loans)
        {
            // the ledger caps the split: debit balances of the receivable accounts, credit balances of the payable and loan accounts (account by account)
            decimal Side(string prefix, bool debit) => accounts.Where(a => a.AccountNumber.StartsWith(prefix, StringComparison.Ordinal))
                .Sum(a => balances.TryGetValue(a.Id, out var b) ? Math.Max(0m, debit ? b.TotalDebit - b.TotalCredit : b.TotalCredit - b.TotalDebit) : 0m);
            var layout = StatementLayouts.For(StatementLayouts.BalanceSheet, accounts)!;
            string[] Prefixes(string special) => layout.Rows.Where(r => r.Special == special).SelectMany(r => r.Terms).Select(x => x.Prefix).Distinct().ToArray();
            decimal Cap(string special, bool debit) => Prefixes(special).Sum(p => Side(p, debit));
            return MaturitySplit.Compute(invoices, bills, loans, asOf, Cap("ShortLessLongReceivable", debit: true), Cap("ShortLessLongPayable", debit: false), Cap("ShortLessLongLoan", debit: false));
        }
    }
}

using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;

namespace ERP.ACC.Application.Posting
{
    /// <summary>
    /// The 14-account chart the platform ships (same numbers as <c>Accounting/001_CreateAccountingDB.sql</c> seeds for the SYSTEM
    /// tenant). A tenant that has no accounts at all gets it the first time something has to be posted; the Vietnamese VAS
    /// chart (ROADMAP P1-2) will replace it by editing accounts and the posting profile.
    /// </summary>
    public static class DefaultChartOfAccounts
    {
        public sealed record Entry(string Number, string Name, AccountType Type, NormalBalance Normal, bool IsControl = false);

        public static readonly IReadOnlyList<Entry> Accounts = new[]
        {
            new Entry("1000", "Cash & Cash Equivalents", AccountType.Asset, NormalBalance.Debit, true),
            new Entry("1100", "Accounts Receivable", AccountType.Asset, NormalBalance.Debit, true),
            new Entry("1200", "Inventory", AccountType.Asset, NormalBalance.Debit),
            new Entry("1500", "Fixed Assets", AccountType.Asset, NormalBalance.Debit),
            new Entry("2000", "Accounts Payable", AccountType.Liability, NormalBalance.Credit, true),
            new Entry("2100", "Accrued Liabilities", AccountType.Liability, NormalBalance.Credit),
            new Entry("2200", "Tax Payable", AccountType.Liability, NormalBalance.Credit),
            new Entry("3000", "Owner Equity", AccountType.Equity, NormalBalance.Credit),
            new Entry("3100", "Retained Earnings", AccountType.Equity, NormalBalance.Credit),
            new Entry("4000", "Sales Revenue", AccountType.Revenue, NormalBalance.Credit),
            new Entry("4100", "Service Revenue", AccountType.Revenue, NormalBalance.Credit),
            new Entry("5000", "Cost of Goods Sold", AccountType.Expense, NormalBalance.Debit),
            new Entry("5100", "Operating Expenses", AccountType.Expense, NormalBalance.Debit),
            new Entry("5200", "Salaries & Benefits", AccountType.Expense, NormalBalance.Debit),
        };

        public static Account ToAccount(Entry e)
        {
            var account = Account.Create(e.Number, e.Name, (int)e.Type, (int)e.Normal);
            account.IsControl = e.IsControl;
            return account;
        }
    }
}

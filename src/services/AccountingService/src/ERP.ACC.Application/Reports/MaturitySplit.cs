using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;

namespace ERP.ACC.Application.Reports
{
    /// <summary>The part of trade receivables (131) and trade payables (331) that falls due after more than 12 months, for one balance-sheet date.</summary>
    public sealed record StatementMaturity(decimal Receivable, decimal Payable, decimal Loan = 0m)
    {
        public bool IsEmpty => Receivable == 0m && Payable == 0m && Loan == 0m;

        /// <summary>Only the invoice / bill parts depend on today's open amounts (loans keep the date of each repayment).</summary>
        public bool HasDocumentParts => Receivable != 0m || Payable != 0m;
    }

    /// <summary>
    /// The short- / long-term split of trade receivables, payables (ROADMAP P1-2h) and borrowings (P1-2i) on the balance sheet B01. At the balance-sheet date D an open invoice or an
    /// approved unpaid bill whose due date is later than D + 12 months is long-term; everything else stays short-term. The result is capped by the ledger (it can
    /// never exceed the debit balance of 131 or the credit balance of 331 or 341), so the split moves amounts between rows and never changes a total.
    /// </summary>
    public static class MaturitySplit
    {
        public const int LongTermMonths = 12;

        public static StatementMaturity Compute(
            IEnumerable<Invoice> invoices, IEnumerable<Bill> bills, DateTime asOf, decimal receivableBalance, decimal payableBalance) =>
            Compute(invoices, bills, Array.Empty<Loan>(), asOf, receivableBalance, payableBalance, 0m);

        /// <summary>
        /// With the loan ledger: the long-term part of borrowings is the principal of the installments due later than D + 12 months that were not repaid on or
        /// before D, capped by the credit balance of the loan account(s) (<paramref name="loanBalance"/>).
        /// </summary>
        public static StatementMaturity Compute(
            IEnumerable<Invoice> invoices, IEnumerable<Bill> bills, IEnumerable<Loan> loans, DateTime asOf,
            decimal receivableBalance, decimal payableBalance, decimal loanBalance)
        {
            var day = asOf.Date;
            var limit = day.AddMonths(LongTermMonths);

            // an invoice is open while Unpaid / Overdue (Paid and Cancelled are settled or void); a bill is a payable once Approved and until Paid;
            // documents created after the date did not exist yet
            var receivable = invoices
                .Where(i => (i.Status == (int)InvoiceStatus.Unpaid || i.Status == (int)InvoiceStatus.Overdue) && i.CreatedAt.Date <= day && i.DueDate.Date > limit)
                .Sum(i => Math.Max(0m, i.Amount - i.AmountPaid));
            var payable = bills
                .Where(b => b.Status == (int)BillStatus.Approved && b.CreatedAt.Date <= day && b.DueDate.Date > limit)
                .Sum(b => Math.Max(0m, b.Amount));

            var loan = loans.Sum(l => l.LongTermAt(day));

            return new StatementMaturity(
                Math.Min(receivable, Math.Max(0m, receivableBalance)),
                Math.Min(payable, Math.Max(0m, payableBalance)),
                Math.Min(loan, Math.Max(0m, loanBalance)));
        }
    }
}

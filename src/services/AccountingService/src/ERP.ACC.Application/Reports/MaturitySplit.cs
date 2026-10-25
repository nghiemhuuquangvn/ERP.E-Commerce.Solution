using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;

namespace ERP.ACC.Application.Reports
{
    /// <summary>
    /// The part of trade receivables (131) and trade payables (331) that falls due after more than 12 months, for one balance-sheet date. <c>Estimated</c> is true when a payment
    /// used for the split has an estimated date, or a document has no payment record (its state today stands in): the split of an earlier date is then approximate.
    /// </summary>
    public sealed record StatementMaturity(decimal Receivable, decimal Payable, decimal Loan = 0m, bool Estimated = false)
    {
        public bool IsEmpty => Receivable == 0m && Payable == 0m && Loan == 0m;

        /// <summary>Only the invoice / bill parts depend on payments (loans keep the date of each repayment).</summary>
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
            decimal receivableBalance, decimal payableBalance, decimal loanBalance) =>
            Compute(invoices, bills, Array.Empty<PaymentApplication>(), loans, asOf, receivableBalance, payableBalance, loanBalance);

        /// <summary>
        /// With the payment register (ROADMAP P1-2j) the open amount of a document on the date is exact: an invoice is open for its amount less the payments made
        /// on or before D (refunds count negative); a bill is a payable from its approval date until the day it was paid. A document with no payment record falls back
        /// to its state today (Paid = settled, otherwise its running total) and the result is flagged <see cref="StatementMaturity.Estimated"/>.
        /// </summary>
        public static StatementMaturity Compute(
            IEnumerable<Invoice> invoices, IEnumerable<Bill> bills, IEnumerable<PaymentApplication> payments, IEnumerable<Loan> loans, DateTime asOf,
            decimal receivableBalance, decimal payableBalance, decimal loanBalance)
        {
            var day = asOf.Date;
            var limit = day.AddMonths(LongTermMonths);
            var byDocument = payments.Where(p => p.PaidOn.Date <= day).ToLookup(p => (p.DocumentType, p.DocumentId));
            var all = payments.ToLookup(p => (p.DocumentType, p.DocumentId));
            var estimated = false;

            // documents created after the date did not exist yet; a cancellation counts from its day (an unknown day = it was always void)
            var receivable = 0m;
            foreach (var i in invoices.Where(i => i.CreatedAt.Date <= day && i.DueDate.Date > limit))
            {
                if (i.Status == (int)InvoiceStatus.Cancelled && (i.CancelledOn == null || i.CancelledOn.Value.Date <= day))
                    continue;

                var key = (PaymentApplication.InvoiceDocument, i.Id);
                var rows = byDocument[key].ToList();
                decimal paid;
                if (all.Contains(key))
                {
                    paid = rows.Sum(p => p.Amount);
                    estimated |= all[key].Any(p => p.IsEstimated);
                }
                else
                {
                    paid = i.Status == (int)InvoiceStatus.Paid ? i.Amount : i.AmountPaid;
                    estimated |= paid > 0m;
                }
                receivable += Math.Max(0m, i.Amount - paid);
            }

            var payable = 0m;
            foreach (var b in bills.Where(b => (b.Status == (int)BillStatus.Approved || b.Status == (int)BillStatus.Paid) && (b.ApprovedOn ?? b.CreatedAt).Date <= day && b.DueDate.Date > limit))
            {
                var key = (PaymentApplication.BillDocument, b.Id);
                bool paidByDate;
                if (all.Contains(key))
                {
                    var rows = byDocument[key].ToList();
                    paidByDate = rows.Sum(p => p.Amount) >= b.Amount;
                    estimated |= all[key].Any(p => p.IsEstimated);
                }
                else
                {
                    paidByDate = b.Status == (int)BillStatus.Paid;
                    estimated |= paidByDate;
                }
                if (!paidByDate)
                    payable += Math.Max(0m, b.Amount);
            }

            var loan = loans.Sum(l => l.LongTermAt(day));

            return new StatementMaturity(
                Math.Min(receivable, Math.Max(0m, receivableBalance)),
                Math.Min(payable, Math.Max(0m, payableBalance)),
                Math.Min(loan, Math.Max(0m, loanBalance)),
                estimated && day < DateTime.UtcNow.Date);
        }
    }
}

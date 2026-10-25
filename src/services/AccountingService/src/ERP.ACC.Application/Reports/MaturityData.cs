using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;

namespace ERP.ACC.Application.Reports
{
    /// <summary>The documents and payments the maturity split of one or two balance-sheet dates needs (shared by B01 and the notes B09).</summary>
    public sealed record MaturityData(List<Invoice> Invoices, List<Bill> Bills, List<PaymentApplication> Payments)
    {
        /// <summary>
        /// Loads what could be long-term on any of the dates: documents due later than the earliest date + 12 months that existed by the latest date
        /// (paid or not today — whether one was open on a date is decided from the payment register), and every payment of those documents.
        /// </summary>
        public static async Task<MaturityData> LoadAsync(
            IInvoiceRepository invoices, IBillRepository bills, IPaymentApplicationRepository payments, IReadOnlyCollection<DateTime> dates, CancellationToken ct)
        {
            var earliest = dates.Min().Date;
            var latest = dates.Max().Date;
            var dueAfter = earliest.AddMonths(MaturitySplit.LongTermMonths);
            return new MaturityData(
                await invoices.GetMaturityCandidatesAsync(latest, dueAfter, ct) ?? new List<Invoice>(),
                await bills.GetMaturityCandidatesAsync(latest, dueAfter, ct) ?? new List<Bill>(),
                await payments.ListForMaturityAsync(latest, dueAfter, ct) ?? new List<PaymentApplication>());
        }
    }
}

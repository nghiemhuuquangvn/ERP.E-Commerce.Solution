using ERP.ACC.Domain;
using ERP.ACC.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.Posting
{
    public static class PostingExtensions
    {
        /// <summary>
        /// Posts, but never lets an incomplete posting profile take the business document down with it: the invoice / bill /
        /// payment is still saved and the missing entry is logged as an error (visible in the log service) so the tenant can
        /// fix its mapping.
        /// </summary>
        public static async Task<JournalEntry?> TryPostAsync(
            this IJournalPostingService posting, ILogger logger, PostingRequest request, CancellationToken ct)
        {
            try
            {
                return await posting.PostAsync(request, ct);
            }
            catch (PostingConfigurationException ex)
            {
                logger.LogError(ex, "No journal entry was posted for {SourceType} {SourceId}: {Reason}",
                    request.SourceType, request.SourceId, ex.Message);
                return null;
            }
        }

        public static async Task<JournalEntry?> TryReverseAsync(
            this IJournalPostingService posting, ILogger logger, string sourceType, Guid sourceId, string reason, DateTime date, CancellationToken ct)
        {
            try
            {
                return await posting.ReverseAsync(sourceType, sourceId, reason, date, ct);
            }
            catch (PostingConfigurationException ex)
            {
                logger.LogError(ex, "The journal entry of {SourceType} {SourceId} was NOT reversed: {Reason}", sourceType, sourceId, ex.Message);
                return null;
            }
        }
    }

    /// <summary>The posting rules of the automatic flows, in one place (see docs/business/Accounting/AutoPosting.md).</summary>
    public static class PostingRules
    {
        public const string Invoice = "Invoice";
        public const string Payment = "Payment";
        public const string InvoicePayment = "InvoicePayment";
        public const string Bill = "Bill";
        public const string BillPayment = "BillPayment";

        /// <summary>Dr Accounts receivable / Cr Sales revenue (gross; VAT is split by P1-3).</summary>
        public static PostingRequest ForInvoice(Domain.Entities.Invoice invoice) =>
            new(Invoice, invoice.Id, invoice.CreatedAt, $"Invoice {invoice.InvoiceNumber}",
                new[]
                {
                    PostingLine.Dr(PostingKeys.AccountsReceivable, invoice.Amount, invoice.InvoiceNumber),
                    PostingLine.Cr(PostingKeys.SalesRevenue, invoice.Amount, invoice.InvoiceNumber),
                });

        /// <summary>Money received for an order: Dr Cash (cash) or Bank (every other method) / Cr Accounts receivable.</summary>
        public static PostingRequest ForPayment(Guid paymentId, Guid orderId, decimal amount, int paymentMethod, DateTime date)
        {
            var key = paymentMethod == CashMethod ? PostingKeys.Cash : PostingKeys.Bank;
            return new(Payment, paymentId, date, $"Payment received for order {orderId:N}",
                new[] { PostingLine.Dr(key, amount), PostingLine.Cr(PostingKeys.AccountsReceivable, amount) });
        }

        /// <summary>A payment recorded by hand on the invoice (no gateway data): Dr Bank / Cr Accounts receivable.</summary>
        public static PostingRequest ForManualInvoicePayment(Domain.Entities.Invoice invoice, decimal outstanding, DateTime date) =>
            new(InvoicePayment, invoice.Id, date, $"Payment of invoice {invoice.InvoiceNumber}",
                new[] { PostingLine.Dr(PostingKeys.Bank, outstanding), PostingLine.Cr(PostingKeys.AccountsReceivable, outstanding) });

        /// <summary>Dr Inventory / Cr Accounts payable. When warehouse receipts post (P1-1b) the debit moves to a GRNI account.</summary>
        public static PostingRequest ForBillApproval(Domain.Entities.Bill bill, DateTime date) =>
            new(Bill, bill.Id, date, $"Bill {bill.BillNumber}",
                new[]
                {
                    PostingLine.Dr(PostingKeys.Inventory, bill.Amount, bill.BillNumber),
                    PostingLine.Cr(PostingKeys.AccountsPayable, bill.Amount, bill.BillNumber),
                });

        /// <summary>Dr Accounts payable / Cr Bank.</summary>
        public static PostingRequest ForBillPayment(Domain.Entities.Bill bill, DateTime date) =>
            new(BillPayment, bill.Id, date, $"Payment of bill {bill.BillNumber}",
                new[]
                {
                    PostingLine.Dr(PostingKeys.AccountsPayable, bill.Amount, bill.BillNumber),
                    PostingLine.Cr(PostingKeys.Bank, bill.Amount, bill.BillNumber),
                });

        /// <summary>PaymentService's PaymentMethod enum: CreditCard 0, DebitCard 1, BankTransfer 2, EWallet 3, Cash 4, Crypto 5.</summary>
        public const int CashMethod = 4;
    }
}

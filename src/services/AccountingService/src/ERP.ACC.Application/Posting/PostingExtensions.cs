using ERP.ACC.Application.EventHandlers;
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
        public const string StockMovement = "StockMovement";
        public const string PosSale = "PosSale";
        public const string PosShift = "PosShift";

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

        /// <summary>Dr Goods received not invoiced / Cr Accounts payable. The stock itself was debited to Inventory by the receipt.</summary>
        public static PostingRequest ForBillApproval(Domain.Entities.Bill bill, DateTime date) =>
            new(Bill, bill.Id, date, $"Bill {bill.BillNumber}",
                new[]
                {
                    PostingLine.Dr(PostingKeys.GoodsReceivedNotInvoiced, bill.Amount, bill.BillNumber),
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

        /// <summary>
        /// One Warehouse stock movement (ROADMAP P1-1b), from its signed stock-value change: a receipt debits Inventory and credits
        /// GRNI; an issue debits COGS and credits Inventory; an adjustment (gain / loss) goes against the InventoryAdjustment key.
        /// Null when the movement changes no value (transfer, unknown cost) — there is nothing to book.
        /// </summary>
        public static PostingRequest? ForStockMovement(Guid movementId, string movementType, decimal valueDelta, DateTime date, string? reference)
        {
            var amount = Math.Abs(Math.Round(valueDelta, 2));
            if (amount == 0m)
                return null;

            string? counterKey = movementType switch
            {
                "Received" => PostingKeys.GoodsReceivedNotInvoiced,
                "Sold" => PostingKeys.CostOfGoodsSold,
                "Adjusted" or "Damaged" => PostingKeys.InventoryAdjustment,
                _ => null,
            };
            if (counterKey == null)
                return null;

            var description = $"Stock {movementType.ToLowerInvariant()}{(string.IsNullOrWhiteSpace(reference) ? "" : " " + reference)}";
            var lines = valueDelta > 0
                ? new[] { PostingLine.Dr(PostingKeys.Inventory, amount), PostingLine.Cr(counterKey, amount) }
                : new[] { PostingLine.Dr(counterKey, amount), PostingLine.Cr(PostingKeys.Inventory, amount) };
            return new(StockMovement, movementId, date, description, lines);
        }

        /// <summary>
        /// A POS sale: Dr Cash (cash tenders) and Dr Bank (every other tender) / Cr Sales revenue (subtotal - discount) and Cr VAT (the
        /// tax POS charged). Null when the amounts are inconsistent (tenders do not add up to the sale total, negative figures), so a
        /// malformed event never books an unbalanced entry.
        /// </summary>
        public static PostingRequest? ForPosSale(
            Guid transactionId, decimal subTotal, decimal discount, decimal tax, IReadOnlyList<PosTenderLine> tenders, DateTime date)
        {
            var revenue = Math.Round(subTotal - discount, 2);
            var vat = Math.Round(tax, 2);
            var total = revenue + vat;
            if (revenue < 0m || vat < 0m || total <= 0m || tenders == null || tenders.Count == 0 || tenders.Any(t => t.Amount < 0m))
                return null;
            if (Math.Round(tenders.Sum(t => t.Amount), 2) != total)
                return null;

            var lines = new List<PostingLine>();
            var cash = Math.Round(tenders.Where(t => string.Equals(t.Method?.Trim(), "Cash", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount), 2);
            var bank = total - cash;
            if (cash > 0m) lines.Add(PostingLine.Dr(PostingKeys.Cash, cash));
            if (bank > 0m) lines.Add(PostingLine.Dr(PostingKeys.Bank, bank));
            if (revenue > 0m) lines.Add(PostingLine.Cr(PostingKeys.SalesRevenue, revenue));
            if (vat > 0m) lines.Add(PostingLine.Cr(PostingKeys.VatOutput, vat));
            return new(PosSale, transactionId, date, $"POS sale {transactionId:N}", lines);
        }

        /// <summary>
        /// Counted cash minus expected cash of a closed POS shift: a shortage (negative) is Dr Cash over / short / Cr Cash, an overage the
        /// reverse. Null when the drawer balanced.
        /// </summary>
        public static PostingRequest? ForPosShiftVariance(Guid shiftId, decimal variance, DateTime date)
        {
            var amount = Math.Abs(Math.Round(variance, 2));
            if (amount == 0m)
                return null;

            var lines = variance < 0m
                ? new[] { PostingLine.Dr(PostingKeys.CashOverShort, amount), PostingLine.Cr(PostingKeys.Cash, amount) }
                : new[] { PostingLine.Dr(PostingKeys.Cash, amount), PostingLine.Cr(PostingKeys.CashOverShort, amount) };
            return new(PosShift, shiftId, date, $"POS shift {shiftId:N} cash {(variance < 0m ? "short" : "over")}", lines);
        }

        /// <summary>PaymentService's PaymentMethod enum: CreditCard 0, DebitCard 1, BankTransfer 2, EWallet 3, Cash 4, Crypto 5.</summary>
        public const int CashMethod = 4;
    }
}

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
        public const string PaySlip = "PaySlip";
        public const string PaySlipPayment = "PaySlipPayment";
        public const string PaymentRefund = "PaymentRefund";
        public const string SalesReturn = "SalesReturn";
        public const string InvoiceVatAdjustment = "InvoiceVatAdjust";
        public const string Loan = "Loan";
        public const string LoanRepayment = "LoanRepayment";
        public const string LoanAccrual = "LoanAccrual";

        /// <summary>Dr Accounts receivable (gross) / Cr Sales revenue (net) and Cr VAT output (the VAT, ROADMAP P1-3); an invoice without VAT posts the two lines as before.</summary>
        public static PostingRequest ForInvoice(Domain.Entities.Invoice invoice)
        {
            var lines = new List<PostingLine>
            {
                PostingLine.Dr(PostingKeys.AccountsReceivable, invoice.Amount, invoice.InvoiceNumber),
                PostingLine.Cr(PostingKeys.SalesRevenue, invoice.Amount - invoice.TaxAmount, invoice.InvoiceNumber),
            };
            if (invoice.TaxAmount > 0m)
                lines.Add(PostingLine.Cr(PostingKeys.VatOutput, invoice.TaxAmount, invoice.InvoiceNumber));
            return new(Invoice, invoice.Id, invoice.CreatedAt, $"Invoice {invoice.InvoiceNumber}", lines);
        }

        /// <summary>
        /// The VAT of an invoice was re-split at another rate: <paramref name="taxDelta"/> more VAT output (positive: Dr Sales revenue / Cr VAT output) or less (negative: the reverse).
        /// One entry per change (the source id is derived from the invoice and the moment).
        /// </summary>
        public static PostingRequest ForInvoiceVatAdjustment(Domain.Entities.Invoice invoice, decimal taxDelta, DateTime date)
        {
            var amount = Math.Abs(taxDelta);
            var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes($"{invoice.Id:N}:{invoice.TaxRatePercent}:{invoice.UpdatedAt.Ticks}"));
            var lines = taxDelta > 0m
                ? new[] { PostingLine.Dr(PostingKeys.SalesRevenue, amount, invoice.InvoiceNumber), PostingLine.Cr(PostingKeys.VatOutput, amount, invoice.InvoiceNumber) }
                : new[] { PostingLine.Dr(PostingKeys.VatOutput, amount, invoice.InvoiceNumber), PostingLine.Cr(PostingKeys.SalesRevenue, amount, invoice.InvoiceNumber) };
            return new(InvoiceVatAdjustment, new Guid(bytes), date.Date, $"VAT of invoice {invoice.InvoiceNumber} set to {invoice.TaxRatePercent}%", lines);
        }

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

        /// <summary>The line on the money side of a loan: the loan's own receiving account when it has one, else the Bank key.</summary>
        private static PostingLine MoneyLine(Domain.Entities.Loan loan, decimal debit, decimal credit) =>
            loan.ReceivingAccountId.HasValue
                ? new PostingLine(null, loan.ReceivingAccountId, debit, credit, loan.LoanNumber)
                : new PostingLine(PostingKeys.Bank, null, debit, credit, loan.LoanNumber);

        /// <summary>A loan drawn (ROADMAP P1-2j): Dr the receiving account (default: Bank) / Cr Loan payable, on the start date.</summary>
        public static PostingRequest ForLoanDrawdown(Domain.Entities.Loan loan) =>
            new(Loan, loan.Id, loan.StartDate, $"Loan {loan.LoanNumber} drawn from {loan.Lender}",
                new[]
                {
                    MoneyLine(loan, loan.Principal, 0m),
                    PostingLine.Cr(PostingKeys.LoanPayable, loan.Principal, loan.LoanNumber),
                });

        /// <summary>
        /// One installment repaid on <paramref name="paidOn"/>: Dr Loan payable (principal), Dr Interest payable (the interest accrued so far, ROADMAP P1-2k), Dr / Cr Interest
        /// expense (the interest paid minus what was accrued: a credit when more was accrued than paid) / Cr the receiving account (principal + interest paid).
        /// Null when the installment moves no money.
        /// </summary>
        public static PostingRequest? ForLoanRepayment(Domain.Entities.Loan loan, Domain.Entities.LoanInstallment installment, DateTime paidOn)
        {
            var principal = Math.Round(installment.PrincipalAmount, 2);
            var interest = Math.Round(installment.InterestAmount, 2);
            var accrued = Math.Round(installment.AccruedInterest, 2);
            if (principal + interest <= 0m && accrued <= 0m)
                return null;

            var lines = new List<PostingLine>();
            if (principal > 0m)
                lines.Add(PostingLine.Dr(PostingKeys.LoanPayable, principal, loan.LoanNumber));
            if (accrued > 0m)
                lines.Add(PostingLine.Dr(PostingKeys.InterestPayable, accrued, loan.LoanNumber));
            var expense = interest - accrued;
            if (expense > 0m)
                lines.Add(PostingLine.Dr(PostingKeys.InterestExpense, expense, loan.LoanNumber));
            else if (expense < 0m)
                lines.Add(PostingLine.Cr(PostingKeys.InterestExpense, -expense, loan.LoanNumber));
            if (principal + interest > 0m)
                lines.Add(MoneyLine(loan, 0m, principal + interest));
            return new PostingRequest(LoanRepayment, installment.Id, paidOn.Date, $"Repayment of loan {loan.LoanNumber} due {installment.DueDate:dd/MM/yyyy}", lines);
        }

        /// <summary>
        /// Interest accrued on an installment up to <paramref name="asOf"/> (ROADMAP P1-2k): Dr Interest expense / Cr Interest payable. One entry per installment and date
        /// (the source id is derived from both, so running the accrual twice for a date posts once).
        /// </summary>
        public static PostingRequest ForLoanAccrual(Domain.Entities.Loan loan, Domain.Entities.LoanInstallment installment, decimal amount, DateTime asOf) =>
            new(LoanAccrual, AccrualSourceId(installment.Id, asOf), asOf.Date, $"Interest accrued on loan {loan.LoanNumber} due {installment.DueDate:dd/MM/yyyy}",
                new[]
                {
                    PostingLine.Dr(PostingKeys.InterestExpense, amount, loan.LoanNumber),
                    PostingLine.Cr(PostingKeys.InterestPayable, amount, loan.LoanNumber),
                });

        /// <summary>A stable id for "this installment accrued up to this date".</summary>
        public static Guid AccrualSourceId(Guid installmentId, DateTime asOf)
        {
            var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes($"{installmentId:N}:{asOf:yyyyMMdd}"));
            return new Guid(bytes);
        }

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

        /// <summary>
        /// A pay slip: Dr Salary expense (gross + overtime) / Cr Salaries payable (net) and Cr Payroll withholdings payable (deductions). Dated
        /// the end of the pay period, at most <paramref name="today"/>, so the expense falls in the month worked. Null when the deductions
        /// exceed the pay.
        /// </summary>
        public static PostingRequest? ForPaySlip(
            Guid paySlipId, string payPeriod, decimal gross, decimal overtime, decimal deductions, DateTime? periodEnd, DateTime today)
        {
            var expense = Math.Round(gross + overtime, 2);
            var withheld = Math.Round(deductions, 2);
            var net = expense - withheld;
            if (expense <= 0m || withheld < 0m || net < 0m)
                return null;

            var lines = new List<PostingLine> { PostingLine.Dr(PostingKeys.SalaryExpense, expense) };
            if (net > 0m) lines.Add(PostingLine.Cr(PostingKeys.SalariesPayable, net));
            if (withheld > 0m) lines.Add(PostingLine.Cr(PostingKeys.PayrollWithholdingsPayable, withheld));

            var date = periodEnd.HasValue && periodEnd.Value.Date < today.Date ? periodEnd.Value.Date : today.Date;
            return new(PaySlip, paySlipId, date, $"Payroll {payPeriod}", lines);
        }

        /// <summary>The net pay was paid out: Dr Salaries payable / Cr Bank. Null for a zero amount.</summary>
        public static PostingRequest? ForPaySlipPayment(Guid paySlipId, string payPeriod, decimal netPay, DateTime paidOn)
        {
            var amount = Math.Round(netPay, 2);
            if (amount <= 0m)
                return null;

            return new(PaySlipPayment, paySlipId, paidOn, $"Payroll {payPeriod} paid",
                new[] { PostingLine.Dr(PostingKeys.SalariesPayable, amount), PostingLine.Cr(PostingKeys.Bank, amount) });
        }

        /// <summary>Money returned to the customer for a confirmed refund: Dr Accounts receivable / Cr Cash (cash) or Bank (every other method).</summary>
        public static PostingRequest ForPaymentRefund(Guid refundId, Guid orderId, decimal amount, int paymentMethod, DateTime date)
        {
            var key = paymentMethod == CashMethod ? PostingKeys.Cash : PostingKeys.Bank;
            return new(PaymentRefund, refundId, date, $"Refund for order {orderId:N}",
                new[] { PostingLine.Dr(PostingKeys.AccountsReceivable, amount), PostingLine.Cr(key, amount) });
        }

        /// <summary>A completed return (credit note): Dr Sales returns / Cr Accounts receivable. Keyed by the order: Sales allows one return per order.</summary>
        public static PostingRequest ForSalesReturn(Guid orderId, string invoiceNumber, decimal amount, DateTime date, decimal taxRatePercent = 0m)
        {
            // the refund is gross: the VAT inside it (at the invoice's rate) comes off the VAT output, the rest off the revenue (ROADMAP P1-3)
            var tax = taxRatePercent > 0m ? VatCalculator.TaxInGross(amount, taxRatePercent) : 0m;
            var lines = new List<PostingLine> { PostingLine.Dr(PostingKeys.SalesReturns, amount - tax, invoiceNumber) };
            if (tax > 0m)
                lines.Add(PostingLine.Dr(PostingKeys.VatOutput, tax, invoiceNumber));
            lines.Add(PostingLine.Cr(PostingKeys.AccountsReceivable, amount, invoiceNumber));
            return new(SalesReturn, orderId, date, $"Return of invoice {invoiceNumber}", lines);
        }

        /// <summary>PaymentService's PaymentMethod enum: CreditCard 0, DebitCard 1, BankTransfer 2, EWallet 3, Cash 4, Crypto 5.</summary>
        public const int CashMethod = 4;
    }
}

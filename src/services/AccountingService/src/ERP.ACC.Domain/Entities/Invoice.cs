using ERP.ACC.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    public class Invoice : AggregateRoot
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public int Status { get; set; }
        public decimal AmountPaid { get; set; }

        /// <summary>Amount before VAT, the VAT rate in percent and the VAT inside <see cref="Amount"/> (the gross); ROADMAP P1-3. Tax 0 for an invoice without VAT.</summary>
        public decimal NetAmount { get; set; }
        public decimal TaxRatePercent { get; set; }
        public decimal TaxAmount { get; set; }

        /// <summary>The part of the net amount that is delivery charged to the customer (ROADMAP P1-11c): booked on the shipping revenue key, the rest on sales revenue.</summary>
        public decimal ShippingNetAmount { get; set; }

        /// <summary>Records how much of the net amount is delivery; it can be neither negative nor more than the net.</summary>
        public void SetShippingNet(decimal shippingNet)
        {
            if (shippingNet < 0m || shippingNet > EffectiveNet)
                throw new ArgumentException($"The delivery part ({shippingNet}) must be between 0 and the net amount ({EffectiveNet}).");
            ShippingNetAmount = Math.Round(shippingNet, 2);
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>The amount before VAT; for an invoice created before VAT existed (net 0, tax 0) the whole amount.</summary>
        public decimal EffectiveNet => NetAmount == 0m && TaxAmount == 0m ? Amount : NetAmount;

        /// <summary>Records the split of the gross <see cref="Amount"/>; net + tax must equal it.</summary>
        public void ApplyVat(decimal net, decimal ratePercent, decimal tax)
        {
            if (net < 0m || tax < 0m || ratePercent < 0m || ratePercent > 100m)
                throw new ArgumentException("The VAT split cannot be negative and the rate is 0–100.");
            if (Math.Abs(net + tax - Amount) > 0.005m)
                throw new ArgumentException($"Net ({net}) plus tax ({tax}) must equal the invoice amount ({Amount}).");
            NetAmount = Math.Round(net, 2);
            TaxRatePercent = ratePercent;
            TaxAmount = Math.Round(tax, 2);
            UpdatedAt = DateTime.UtcNow;
        }


        /// <summary>Currency of a foreign document (null = the base currency), the rate (base per 1 foreign) and the gross in that currency; <see cref="Amount"/> is always in the base currency (ROADMAP P1-4).</summary>
        public string? ForeignCurrencyCode { get; set; }
        public decimal? ExchangeRate { get; set; }
        public decimal? ForeignAmount { get; set; }
        public bool IsForeign => !string.IsNullOrEmpty(ForeignCurrencyCode) && ExchangeRate.HasValue && ForeignAmount.HasValue;

        /// <summary>Marks the document as a foreign one: the gross in <paramref name="currency"/> and the rate it is converted at.</summary>
        public void SetForeign(string currency, decimal foreignAmount, decimal rate)
        {
            if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
                throw new ArgumentException("The currency is a 3-letter code.", nameof(currency));
            if (rate <= 0m)
                throw new ArgumentException("The exchange rate must be greater than zero.", nameof(rate));
            if (foreignAmount < 0m)
                throw new ArgumentException("The foreign amount cannot be negative.", nameof(foreignAmount));
            ForeignCurrencyCode = currency.Trim().ToUpperInvariant();
            ExchangeRate = rate;
            ForeignAmount = Math.Round(foreignAmount, 2);
        }

        /// <summary>The day the invoice was cancelled (null while it is not, or for a cancellation made before this was recorded).</summary>
        public DateTime? CancelledOn { get; set; }

        public static Invoice Create(string invoiceNumber, Guid customerId, Guid orderId, decimal amount, DateTime dueDate)
        {
            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = invoiceNumber,
                CustomerId = customerId,
                OrderId = orderId,
                Amount = amount,
                DueDate = dueDate,
                Status = 0,
                CreatedAt = DateTime.UtcNow
            };

            invoice.AddDomainEvent(new InvoiceCreatedEvent(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.CustomerId,
                invoice.OrderId,
                invoice.Amount,
                invoice.DueDate));

            return invoice;
        }

        /// <summary>
        /// Books a payment against the invoice; the invoice is paid once the payments add up to its amount. A cancelled or
        /// already paid invoice takes nothing (the cash is still recorded in the ledger by the payment posting).
        /// </summary>
        public bool ApplyPayment(decimal amount)
        {
            if (Status == (int)Enums.InvoiceStatus.Paid || Status == (int)Enums.InvoiceStatus.Cancelled || amount <= 0)
                return false;

            AmountPaid += amount;
            if (AmountPaid >= Amount)
                MarkPaid();
            else
                UpdatedAt = DateTime.UtcNow;
            return true;
        }

        /// <summary>
        /// Money paid for this invoice went back to the customer (a confirmed Payment refund): the paid amount goes down and a paid invoice
        /// that is no longer covered is open again. A cancelled invoice keeps its status.
        /// </summary>
        public void ApplyRefund(decimal amount)
        {
            if (amount <= 0)
                return;

            AmountPaid = Math.Max(0m, AmountPaid - amount);
            if (Status == (int)Enums.InvoiceStatus.Paid && AmountPaid < Amount)
                Status = (int)Enums.InvoiceStatus.Unpaid;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// The order was cancelled. A paid invoice can be cancelled too: its cash stays in the ledger as a credit owed to the customer
        /// (AmountPaid is kept) until the Payment refund is booked.
        /// </summary>
        public void Cancel()
        {
            if (Status == (int)Enums.InvoiceStatus.Cancelled)
                return;

            Status = (int)Enums.InvoiceStatus.Cancelled;
            CancelledOn = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkPaid()
        {
            if (Status == 1)
                throw new InvalidOperationException($"Invoice {InvoiceNumber} is already paid.");
            if (Status == (int)Enums.InvoiceStatus.Cancelled)
                throw new InvalidOperationException($"Invoice {InvoiceNumber} is cancelled.");

            Status = 1;
            if (AmountPaid < Amount)
                AmountPaid = Amount;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new InvoicePaidEvent(
                Id,
                InvoiceNumber,
                CustomerId,
                Amount,
                DateTime.UtcNow));
        }
    }
}

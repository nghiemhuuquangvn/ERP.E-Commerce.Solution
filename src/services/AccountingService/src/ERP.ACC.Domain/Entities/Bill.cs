using ERP.ACC.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    public class Bill : AggregateRoot
    {
        public string BillNumber { get; set; } = string.Empty;
        public Guid SupplierId { get; set; }
        public Guid POId { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public int Status { get; set; }

        /// <summary>Amount before VAT, the VAT rate in percent and the input VAT inside <see cref="Amount"/> (the gross owed to the supplier); ROADMAP P1-3.</summary>
        public decimal NetAmount { get; set; }
        public decimal TaxRatePercent { get; set; }
        public decimal TaxAmount { get; set; }

        /// <summary>The amount before VAT; for a bill created before VAT existed (net 0, tax 0) the whole amount.</summary>
        public decimal EffectiveNet => NetAmount == 0m && TaxAmount == 0m ? Amount : NetAmount;

        /// <summary>Records the split of the gross <see cref="Amount"/>; net + tax must equal it. Only a pending bill can be re-split (an approved one is posted).</summary>
        public void ApplyVat(decimal net, decimal ratePercent, decimal tax)
        {
            if (net < 0m || tax < 0m || ratePercent < 0m || ratePercent > 100m)
                throw new ArgumentException("The VAT split cannot be negative and the rate is 0–100.");
            if (Math.Abs(net + tax - Amount) > 0.005m)
                throw new ArgumentException($"Net ({net}) plus tax ({tax}) must equal the bill amount ({Amount}).");
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

        /// <summary>The supplier invoice (Purchasing, ROADMAP P1-8) this bill was booked from; null for a bill entered by hand.</summary>
        public Guid? SupplierInvoiceId { get; set; }

        /// <summary>
        /// The goods of the invoice at the order price - what the receipts credited to GRNI. The difference to the net amount is the purchase price
        /// variance. Null for a bill that is not from a supplier invoice (it clears GRNI by its whole net amount).
        /// </summary>
        public decimal? ReceivedValue { get; set; }

        /// <summary>What the supplier credited back (debit notes of returned goods); the open balance is <see cref="Outstanding"/>.</summary>
        public decimal CreditedAmount { get; set; }

        /// <summary>What is still to be paid: the amount less the credits.</summary>
        public decimal Outstanding => Math.Max(0m, Amount - CreditedAmount);

        /// <summary>Takes a supplier credit off the open balance; returns what was applied (never more than is outstanding).</summary>
        public decimal ApplyCredit(decimal credit)
        {
            if (credit <= 0m) return 0m;
            var applied = Math.Min(credit, Outstanding);
            CreditedAmount += applied;
            UpdatedAt = DateTime.UtcNow;
            return applied;
        }

        /// <summary>The day the bill was approved, i.e. became a payable (null for a bill approved before this was recorded: its creation date is used).</summary>
        public DateTime? ApprovedOn { get; set; }

        public static Bill Create(string billNumber, Guid supplierId, Guid poId, decimal amount, DateTime dueDate)
        {
            var bill = new Bill
            {
                Id = Guid.NewGuid(),
                BillNumber = billNumber,
                SupplierId = supplierId,
                POId = poId,
                Amount = amount,
                DueDate = dueDate,
                Status = 0,
                CreatedAt = DateTime.UtcNow
            };

            return bill;
        }

        public void Approve()
        {
            if (Status != 0)
                throw new InvalidOperationException($"Bill {BillNumber} is not in Pending status.");

            Status = 1;
            ApprovedOn = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new BillApprovedEvent(Id, BillNumber, SupplierId, POId, Amount));
        }

        public void MarkPaid()
        {
            if (Status != 1)
                throw new InvalidOperationException($"Bill {BillNumber} must be Approved before it can be marked as Paid.");

            Status = 2;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new BillPaidEvent(Id, BillNumber, SupplierId, Amount, DateTime.UtcNow));
        }
    }
}

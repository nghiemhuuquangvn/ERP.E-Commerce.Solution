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

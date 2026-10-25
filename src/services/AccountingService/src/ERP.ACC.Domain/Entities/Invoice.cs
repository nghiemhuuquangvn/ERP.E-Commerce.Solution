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

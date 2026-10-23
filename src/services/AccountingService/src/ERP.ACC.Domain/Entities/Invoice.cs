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

        public void Cancel()
        {
            if (Status == (int)Enums.InvoiceStatus.Paid)
                throw new InvalidOperationException($"Invoice {InvoiceNumber} is paid and cannot be cancelled.");
            if (Status == (int)Enums.InvoiceStatus.Cancelled)
                return;

            Status = (int)Enums.InvoiceStatus.Cancelled;
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

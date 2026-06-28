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

        public void MarkPaid()
        {
            if (Status == 1)
                throw new InvalidOperationException($"Invoice {InvoiceNumber} is already paid.");

            Status = 1;
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

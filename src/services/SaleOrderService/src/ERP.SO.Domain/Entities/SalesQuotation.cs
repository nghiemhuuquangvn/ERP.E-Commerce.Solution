using ERP.Shared.Core.Domain;
using ERP.SO.Domain.Events;
using ERP.SO.Domain.ValueObjects;

namespace ERP.SO.Domain.Entities
{
    public class SalesQuotation : AggregateRoot
    {
        public Guid CustomerId { get; private set; }
        public string CustomerName { get; private set; } = string.Empty;
        public string CustomerEmail { get; private set; } = string.Empty;
        public string QuotationNo { get; private set; } = string.Empty;
        public QuotationStatus Status { get; private set; }
        public DateTime ValidUntil { get; private set; }
        public decimal TotalAmount { get; private set; }
        public Guid? ConvertedOrderId { get; private set; }

        private List<QuotationLineItem> _lineItems = new();
        public IReadOnlyCollection<QuotationLineItem> LineItems => _lineItems.AsReadOnly();

        protected SalesQuotation() { }

        public static SalesQuotation Create(
            Guid customerId,
            string customerName,
            string customerEmail,
            List<QuotationLineItem> lineItems,
            DateTime validUntil,
            string quotationNo)
        {
            if (lineItems.Count == 0)
                throw new ArgumentException("A quotation must have at least one line item", nameof(lineItems));

            var quotation = new SalesQuotation
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                CustomerName = customerName,
                CustomerEmail = customerEmail,
                QuotationNo = quotationNo,
                Status = QuotationStatus.Draft,
                ValidUntil = validUntil,
                _lineItems = lineItems
            };

            quotation.TotalAmount = lineItems.Sum(li => li.LineTotal);

            quotation.AddDomainEvent(new SalesQuotationCreatedEvent
            {
                QuotationId = quotation.Id,
                CustomerId = customerId,
                TotalAmount = quotation.TotalAmount,
                CreatedAt = DateTime.UtcNow
            });

            return quotation;
        }

        public void Send()
        {
            if (Status != QuotationStatus.Draft)
                throw new InvalidOperationException($"Cannot send quotation in {Status} status");

            Status = QuotationStatus.Sent;
        }

        public void Accept()
        {
            if (Status != QuotationStatus.Sent)
                throw new InvalidOperationException($"Cannot accept quotation in {Status} status");
            if (DateTime.UtcNow > ValidUntil)
                throw new InvalidOperationException("Cannot accept an expired quotation");

            Status = QuotationStatus.Accepted;

            AddDomainEvent(new SalesQuotationAcceptedEvent
            {
                QuotationId = Id,
                CustomerId = CustomerId,
                TotalAmount = TotalAmount,
                AcceptedAt = DateTime.UtcNow
            });
        }

        public void Reject()
        {
            if (Status != QuotationStatus.Sent)
                throw new InvalidOperationException($"Cannot reject quotation in {Status} status");

            Status = QuotationStatus.Rejected;
        }

        public void MarkExpired()
        {
            if (Status is QuotationStatus.Accepted or QuotationStatus.Converted or QuotationStatus.Rejected)
                throw new InvalidOperationException($"Cannot expire quotation in {Status} status");

            Status = QuotationStatus.Expired;
        }

        /// <summary>Records that a SalesOrder was created from this quotation — the order itself is created by the application handler, not here, since an aggregate shouldn't construct a different aggregate root.</summary>
        public void MarkConverted(Guid orderId)
        {
            if (Status != QuotationStatus.Accepted)
                throw new InvalidOperationException($"Cannot convert a quotation in {Status} status — it must be Accepted first");

            Status = QuotationStatus.Converted;
            ConvertedOrderId = orderId;
        }
    }
}

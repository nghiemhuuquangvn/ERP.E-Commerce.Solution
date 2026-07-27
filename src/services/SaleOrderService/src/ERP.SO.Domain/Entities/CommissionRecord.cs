namespace ERP.SO.Domain.Entities
{
    public enum CommissionStatus
    {
        Pending = 0,
        Approved = 1,
        Paid = 2
    }

    /// <summary>
    /// Created only when PaymentProcessedEvent arrives for an order with a SalesRepId set —
    /// matches the business rule "commission only on paid orders, never on merely-confirmed
    /// orders" using the event that actually exists in this system (there is no InvoicePaid
    /// event anywhere).
    /// </summary>
    public class CommissionRecord
    {
        public Guid Id { get; private set; }
        public Guid PlanId { get; private set; }
        public Guid SalesRepId { get; private set; }
        public Guid OrderId { get; private set; }
        public decimal Amount { get; private set; }
        public DateTime CalculatedAt { get; private set; }
        public CommissionStatus Status { get; private set; }

        protected CommissionRecord() { }

        public static CommissionRecord Create(Guid planId, Guid salesRepId, Guid orderId, decimal amount)
        {
            return new CommissionRecord
            {
                Id = Guid.NewGuid(),
                PlanId = planId,
                SalesRepId = salesRepId,
                OrderId = orderId,
                Amount = amount,
                CalculatedAt = DateTime.UtcNow,
                Status = CommissionStatus.Pending
            };
        }

        public void Approve()
        {
            if (Status != CommissionStatus.Pending)
                throw new InvalidOperationException($"Cannot approve commission in {Status} status");
            Status = CommissionStatus.Approved;
        }

        public void MarkPaid()
        {
            if (Status != CommissionStatus.Approved)
                throw new InvalidOperationException($"Cannot mark paid a commission in {Status} status");
            Status = CommissionStatus.Paid;
        }
    }
}

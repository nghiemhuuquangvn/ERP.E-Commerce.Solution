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

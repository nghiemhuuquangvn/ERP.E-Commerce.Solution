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
    }
}

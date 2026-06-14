using ERP.Shared.Core.Domain;

namespace ERP.HRM.Domain.Entities
{
    public class PaySlip : AggregateRoot
    {
        public string PaySlipNumber { get; set; } = string.Empty;
        public Guid EmployeeId { get; set; }
        public decimal GrossSalary { get; set; }
        public decimal Deductions { get; set; }
        public decimal NetSalary { get; set; }
        public DateTime PaymentDate { get; set; }
        public int PaymentStatus { get; set; }

        public static PaySlip Create(string paySlipNumber, Guid employeeId, decimal grossSalary, decimal deductions)
        {
            var paySlip = new PaySlip
            {
                Id = Guid.NewGuid(),
                PaySlipNumber = paySlipNumber,
                EmployeeId = employeeId,
                GrossSalary = grossSalary,
                Deductions = deductions,
                NetSalary = grossSalary - deductions,
                PaymentStatus = 0,
                CreatedAt = DateTime.UtcNow
            };

            return paySlip;
        }
    }
}

using ERP.HRM.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.HRM.Domain.Entities
{
    public class PaySlip : AggregateRoot
    {
        public string PaySlipNumber { get; set; } = string.Empty;
        public Guid EmployeeId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal GrossSalary { get; set; }
        public decimal Deductions { get; set; }
        public decimal NetSalary { get; set; }
        public DateTime PaymentDate { get; set; }
        public int PaymentStatus { get; set; }

        public string PayPeriod => $"{Year:D4}-{Month:D2}";

        public static PaySlip Create(
            string paySlipNumber,
            Guid employeeId,
            int year,
            int month,
            decimal grossSalary,
            decimal deductions)
        {
            var paySlip = new PaySlip
            {
                Id = Guid.NewGuid(),
                PaySlipNumber = paySlipNumber,
                EmployeeId = employeeId,
                Year = year,
                Month = month,
                GrossSalary = grossSalary,
                Deductions = deductions,
                NetSalary = grossSalary - deductions,
                PaymentDate = new DateTime(year, month, 1).AddMonths(1).AddDays(-1),
                PaymentStatus = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            paySlip.AddDomainEvent(new PaySlipGeneratedEvent(
                paySlip.Id,
                paySlip.EmployeeId,
                paySlip.NetSalary,
                paySlip.PayPeriod));

            return paySlip;
        }
    }
}

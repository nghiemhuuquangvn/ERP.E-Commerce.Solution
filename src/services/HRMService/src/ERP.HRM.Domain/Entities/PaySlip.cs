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
        public decimal OvertimeHours { get; set; }
        public decimal OvertimePay { get; set; }
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
            decimal deductions,
            decimal overtimeHours = 0m,
            decimal overtimePay = 0m)
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
                OvertimeHours = overtimeHours,
                OvertimePay = overtimePay,
                NetSalary = grossSalary + overtimePay - deductions,
                PaymentDate = new DateTime(year, month, 1).AddMonths(1).AddDays(-1),
                PaymentStatus = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            paySlip.AddDomainEvent(new PaySlipGeneratedEvent(
                paySlip.Id,
                paySlip.EmployeeId,
                paySlip.NetSalary,
                paySlip.PayPeriod,
                paySlip.GrossSalary,
                paySlip.OvertimePay,
                paySlip.Deductions,
                paySlip.PaymentDate));

            return paySlip;
        }

        /// <summary>Net pay was paid out. Only a pending slip can be paid, once.</summary>
        public void MarkPaid(DateTime paidOn, string? paymentMethod = null)
        {
            if (PaymentStatus != (int)Enums.PaymentStatus.Pending)
                throw new InvalidOperationException($"Pay slip {PaySlipNumber} is {(Enums.PaymentStatus)PaymentStatus}; only a pending pay slip can be paid.");

            PaymentStatus = (int)Enums.PaymentStatus.Paid;
            PaymentDate = paidOn;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new PaySlipPaidEvent(Id, EmployeeId, NetSalary, PayPeriod, paidOn, paymentMethod));
        }

        /// <summary>An unpaid pay slip is withdrawn (wrong figures): a paid one needs a payroll correction, not a cancellation.</summary>
        public void Cancel(string reason)
        {
            if (PaymentStatus != (int)Enums.PaymentStatus.Pending)
                throw new InvalidOperationException($"Pay slip {PaySlipNumber} is {(Enums.PaymentStatus)PaymentStatus}; only a pending pay slip can be cancelled.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A reason is required to cancel a pay slip.", nameof(reason));

            PaymentStatus = (int)Enums.PaymentStatus.Cancelled;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new PaySlipVoidedEvent(Id, EmployeeId, PayPeriod, reason.Trim()));
        }
    }
}

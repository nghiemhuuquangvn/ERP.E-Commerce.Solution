using ERP.HRM.Domain.Events;
using ERP.HRM.Domain.Payroll;
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

        // Calculation breakdown (ROADMAP P1-7). All zero on a slip entered by hand; Deductions is then the typed lump.
        public decimal BaseSalary { get; set; }
        public decimal Allowances { get; set; }
        public decimal WorkedDays { get; set; }
        public decimal StandardDays { get; set; }
        public decimal EmployeeSocial { get; set; }
        public decimal EmployeeHealth { get; set; }
        public decimal EmployeeUnemployment { get; set; }
        public decimal EmployerSocial { get; set; }
        public decimal EmployerHealth { get; set; }
        public decimal EmployerUnemployment { get; set; }
        public decimal TaxableIncome { get; set; }
        public decimal PersonalIncomeTax { get; set; }
        public int DependentCount { get; set; }
        public Guid? RateSetId { get; set; }

        public decimal EmployerInsurance => EmployerSocial + EmployerHealth + EmployerUnemployment;

        public string PayPeriod => $"{Year:D4}-{Month:D2}";

        public static PaySlip Create(
            string paySlipNumber,
            Guid employeeId,
            int year,
            int month,
            decimal grossSalary,
            decimal deductions,
            decimal overtimeHours = 0m,
            decimal overtimePay = 0m,
            bool raiseEvent = true)
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

            if (raiseEvent) paySlip.RaiseGenerated();

            return paySlip;
        }

        /// <summary>A pay slip calculated from the employee's salary terms and the rate set in force (insurance, income tax, worked days).</summary>
        public static PaySlip CreateCalculated(string paySlipNumber, Guid employeeId, int year, int month, PayrollResult r)
        {
            var paySlip = Create(paySlipNumber, employeeId, year, month, r.GrossSalary, r.Deductions, r.OvertimeHours, r.OvertimePay, raiseEvent: false);
            paySlip.BaseSalary = r.BaseSalary;
            paySlip.Allowances = r.Allowances;
            paySlip.WorkedDays = r.PaidDays;
            paySlip.StandardDays = r.StandardDays;
            paySlip.EmployeeSocial = r.EmployeeSocial;
            paySlip.EmployeeHealth = r.EmployeeHealth;
            paySlip.EmployeeUnemployment = r.EmployeeUnemployment;
            paySlip.EmployerSocial = r.EmployerSocial;
            paySlip.EmployerHealth = r.EmployerHealth;
            paySlip.EmployerUnemployment = r.EmployerUnemployment;
            paySlip.TaxableIncome = r.TaxableIncome;
            paySlip.PersonalIncomeTax = r.PersonalIncomeTax;
            paySlip.DependentCount = r.DependentCount;
            paySlip.RateSetId = r.RateSetId;
            paySlip.RaiseGenerated();
            return paySlip;
        }

        private void RaiseGenerated() => AddDomainEvent(new PaySlipGeneratedEvent(
            Id, EmployeeId, NetSalary, PayPeriod, GrossSalary, OvertimePay, Deductions, PaymentDate,
            EmployeeSocial, EmployeeHealth, EmployeeUnemployment, EmployerSocial, EmployerHealth, EmployerUnemployment, PersonalIncomeTax));

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

using ERP.ACC.Domain.Enums;
using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    /// <summary>One repayment of a loan: the principal due on a date and, once repaid, the date it was paid.</summary>
    public class LoanInstallment : AggregateRoot
    {
        public Guid LoanId { get; private set; }
        public DateTime DueDate { get; private set; }
        public decimal PrincipalAmount { get; private set; }
        public decimal InterestAmount { get; private set; }

        /// <summary>The day the principal was repaid; null while it is outstanding.</summary>
        public DateTime? PaidOn { get; private set; }

        /// <summary>Interest of this installment already accrued to the books (Dr interest expense / Cr interest payable), ROADMAP P1-2k. Cleared by the repayment.</summary>
        public decimal AccruedInterest { get; private set; }

        private LoanInstallment() { }

        internal static LoanInstallment Create(Guid loanId, DateTime dueDate, decimal principal, decimal interest) => new()
        {
            Id = Guid.NewGuid(),
            LoanId = loanId,
            DueDate = dueDate.Date,
            PrincipalAmount = Math.Round(principal, 2),
            InterestAmount = Math.Round(interest, 2),
            CreatedAt = DateTime.UtcNow,
        };

        internal void Accrue(decimal delta)
        {
            AccruedInterest = Math.Round(AccruedInterest + delta, 2);
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>Marks the installment repaid; the interest actually paid (when given) replaces the scheduled interest.</summary>
        internal void MarkPaid(DateTime paidOn, decimal? interestPaid = null)
        {
            if (interestPaid.HasValue)
                InterestAmount = Math.Round(interestPaid.Value, 2);
            PaidOn = paidOn.Date;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// A borrowing with its repayment schedule (ROADMAP P1-2i). The schedule gives B01 the real due dates, and because each repayment is stored with the date
    /// it was made, the amount outstanding — and the part due after 12 months — is exact for any past date, unlike invoices. The notes are
    /// <see cref="BaseEntity.Description"/>. Drawing and repaying a loan are posted to the journal (ROADMAP P1-2j) unless the loan was booked by hand; B01 is still capped by the ledger.
    /// </summary>
    public class Loan : AggregateRoot
    {
        public string LoanNumber { get; private set; } = string.Empty;
        public string Lender { get; private set; } = string.Empty;
        public decimal Principal { get; private set; }

        /// <summary>Annual rate in percent; information only — the interest that is posted is the interest of each scheduled installment.</summary>
        public decimal InterestRatePercent { get; private set; }
        public DateTime StartDate { get; private set; }
        public int Status { get; private set; } = (int)LoanStatus.Active;
        public string? CancelReason { get; private set; }

        /// <summary>True once the drawdown is in the journal: from then on repayments are posted by the ledger too (ROADMAP P1-2j). False for a loan booked by hand.</summary>
        public bool PostedToLedger { get; private set; }

        /// <summary>The asset account (a bank or cash account) the money is received on and repaid from; null = the Bank key of the posting profile (ROADMAP P1-2k).</summary>
        public Guid? ReceivingAccountId { get; private set; }

        /// <summary>A <see cref="LoanDayCount"/>: 0 = the scheduled interest split by the days of its period (default); 1 / 2 = outstanding principal × annual rate ÷ 365 / 360 per day (ROADMAP P1-2l).</summary>
        public int DayCountBasis { get; private set; }

        public void SetDayCountBasis(int basis)
        {
            if (!Enum.IsDefined(typeof(LoanDayCount), basis))
                throw new ArgumentException("The day count must be 0 (schedule), 1 (actual/365) or 2 (actual/360).", nameof(basis));
            if (basis != (int)LoanDayCount.Schedule && InterestRatePercent <= 0m)
                throw new ArgumentException("An interest rate above 0 is needed to count the interest by days.", nameof(basis));
            if (Installments.Any(i => i.AccruedInterest > 0m || i.PaidOn.HasValue))
                throw new InvalidOperationException("The day count cannot change once interest was accrued or a repayment recorded.");
            DayCountBasis = basis;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetReceivingAccount(Guid? accountId)
        {
            ReceivingAccountId = accountId == Guid.Empty ? null : accountId;
            UpdatedAt = DateTime.UtcNow;
        }
        public List<LoanInstallment> Installments { get; private set; } = new();

        private Loan() { }

        public void MarkPosted()
        {
            PostedToLedger = true;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// The schedule is a list of (due date, principal, interest); its principals must add up to the loan principal and fall after the start date.
        /// A loan with no schedule is one installment of the whole principal on <paramref name="maturityDate"/>.
        /// </summary>
        public static Loan Create(
            string loanNumber, string lender, decimal principal, decimal interestRatePercent, DateTime startDate,
            IEnumerable<(DateTime DueDate, decimal Principal, decimal Interest)>? schedule, DateTime? maturityDate, string? notes, int dayCountBasis = 0)
        {
            if (string.IsNullOrWhiteSpace(loanNumber))
                throw new ArgumentException("A loan needs a number.", nameof(loanNumber));
            if (string.IsNullOrWhiteSpace(lender))
                throw new ArgumentException("A loan needs a lender.", nameof(lender));
            if (principal <= 0m)
                throw new ArgumentException("The principal must be greater than 0.", nameof(principal));
            if (interestRatePercent < 0m)
                throw new ArgumentException("The interest rate cannot be negative.", nameof(interestRatePercent));

            var loan = new Loan
            {
                Id = Guid.NewGuid(),
                LoanNumber = loanNumber.Trim(),
                Lender = lender.Trim(),
                Principal = Math.Round(principal, 2),
                InterestRatePercent = interestRatePercent,
                StartDate = startDate.Date,
                Description = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                CreatedAt = DateTime.UtcNow,
            };

            var lines = schedule?.ToList() ?? new List<(DateTime, decimal, decimal)>();
            if (lines.Count == 0)
            {
                if (!maturityDate.HasValue)
                    throw new ArgumentException("A loan needs a repayment schedule or a maturity date.", nameof(schedule));
                lines.Add((maturityDate.Value, principal, 0m));
            }

            if (lines.Any(l => l.Item2 <= 0m || l.Item3 < 0m))
                throw new ArgumentException("Each installment needs a principal above 0 and an interest of 0 or more.", nameof(schedule));
            if (lines.Any(l => l.Item1.Date <= loan.StartDate))
                throw new ArgumentException("Every due date must be after the start date.", nameof(schedule));
            if (Math.Abs(lines.Sum(l => l.Item2) - principal) > 0.01m)
                throw new ArgumentException($"The installments' principal ({lines.Sum(l => l.Item2)}) must add up to the loan principal ({principal}).", nameof(schedule));

            foreach (var (due, p, i) in lines.OrderBy(l => l.Item1))
                loan.Installments.Add(LoanInstallment.Create(loan.Id, due, p, i));
            if (dayCountBasis != (int)LoanDayCount.Schedule)
                loan.SetDayCountBasis(dayCountBasis);
            return loan;
        }

        public LoanInstallment RecordRepayment(Guid installmentId, DateTime paidOn, decimal? interestPaid = null)
        {
            if (interestPaid is < 0m)
                throw new ArgumentException("The interest paid cannot be negative.", nameof(interestPaid));
            if (Status != (int)LoanStatus.Active)
                throw new InvalidOperationException($"Loan {LoanNumber} is {(LoanStatus)Status}: no repayment can be recorded.");
            var installment = Installments.FirstOrDefault(i => i.Id == installmentId)
                ?? throw new InvalidOperationException("The installment does not belong to this loan.");
            if (installment.PaidOn.HasValue)
                throw new InvalidOperationException($"The installment due {installment.DueDate:yyyy-MM-dd} is already repaid.");
            if (paidOn.Date < StartDate)
                throw new ArgumentException("A repayment cannot be dated before the loan started.", nameof(paidOn));

            // a loan counted by days owes the interest of the days to the payment, unless the lender's figure is given
            if (!interestPaid.HasValue && DayCountBasis != (int)LoanDayCount.Schedule)
                interestPaid = InterestToDate(installment, paidOn);
            installment.MarkPaid(paidOn, interestPaid);
            if (Installments.All(i => i.PaidOn.HasValue))
                Status = (int)LoanStatus.Repaid;
            UpdatedAt = DateTime.UtcNow;
            return installment;
        }

        /// <summary>
        /// Accrues the interest of the unpaid installments up to <paramref name="asOf"/> (ROADMAP P1-2k): in each installment's period (the previous due date, or the loan start,
        /// up to its due date) the interest accrues by calendar days. Returns the installments that gained interest with the amount; running it again for the same or an
        /// earlier date gains nothing.
        /// </summary>
        public List<(LoanInstallment Installment, decimal Amount)> AccrueInterest(DateTime asOf)
        {
            var result = new List<(LoanInstallment, decimal)>();
            if (Status != (int)LoanStatus.Active)
                return result;

            var day = asOf.Date;
            var previousDue = StartDate;
            foreach (var installment in Installments.OrderBy(i => i.DueDate))
            {
                var periodStart = previousDue;
                previousDue = installment.DueDate;
                var byDays = DayCountBasis != (int)LoanDayCount.Schedule;
                if (installment.PaidOn.HasValue || (!byDays && installment.InterestAmount <= 0m) || day <= periodStart)
                    continue;

                decimal target;
                if (byDays)
                {
                    target = InterestOfPeriod(periodStart, day < installment.DueDate ? day : installment.DueDate);
                }
                else
                {
                    var days = Math.Max(1, (installment.DueDate - periodStart).Days);
                    var fraction = Math.Min(1m, (decimal)(day - periodStart).Days / days);
                    target = Math.Round(installment.InterestAmount * fraction, 2);
                }
                var delta = target - installment.AccruedInterest;
                if (delta <= 0m)
                    continue;
                installment.Accrue(delta);
                result.Add((installment, delta));
            }
            if (result.Count > 0)
                UpdatedAt = DateTime.UtcNow;
            return result;
        }

        /// <summary>
        /// The interest owed on an installment up to <paramref name="date"/> (its period starts at the previous due date or the loan start). A loan counted by days: the
        /// outstanding principal of each day × rate ÷ 365 / 360; a scheduled loan: the scheduled interest by the share of the period's days.
        /// </summary>
        public decimal InterestToDate(LoanInstallment installment, DateTime date)
        {
            var periodStart = StartDate;
            foreach (var i in Installments.OrderBy(i => i.DueDate))
            {
                if (i == installment)
                    break;
                periodStart = i.DueDate;
            }
            var end = date.Date < installment.DueDate ? date.Date : installment.DueDate;
            if (end <= periodStart)
                return 0m;
            if (DayCountBasis == (int)LoanDayCount.Schedule)
            {
                var days = Math.Max(1, (installment.DueDate - periodStart).Days);
                return Math.Round(installment.InterestAmount * Math.Min(1m, (decimal)(end - periodStart).Days / days), 2);
            }
            return InterestOfPeriod(periodStart, end);
        }

        /// <summary>Σ over the days in [from, to) of outstanding principal × rate ÷ basis; a principal repaid on a day no longer earns interest from that day.</summary>
        private decimal InterestOfPeriod(DateTime from, DateTime to)
        {
            var basis = DayCountBasis == (int)LoanDayCount.Actual360 ? 360m : 365m;
            var paid = Installments.Where(i => i.PaidOn.HasValue).Select(i => (On: i.PaidOn!.Value.Date, i.PrincipalAmount)).ToList();
            var perDay = InterestRatePercent / 100m / basis;
            var total = 0m;
            for (var d = from.Date; d < to.Date; d = d.AddDays(1))
            {
                var outstanding = Principal - paid.Where(p => p.On <= d).Sum(p => p.PrincipalAmount);
                if (outstanding > 0m)
                    total += outstanding * perDay;
            }
            return Math.Round(total, 2);
        }

        /// <summary>Takes an accrual back (the entry could not be posted, so the interest must not be remembered as accrued).</summary>
        public void UndoAccrual(LoanInstallment installment, decimal amount)
        {
            if (Installments.Contains(installment))
                installment.Accrue(-amount);
        }

        /// <summary>A loan entered by mistake can be cancelled while nothing has been repaid.</summary>
        public void Cancel(string reason)
        {
            if (Status != (int)LoanStatus.Active)
                throw new InvalidOperationException($"Loan {LoanNumber} is {(LoanStatus)Status} and cannot be cancelled.");
            if (Installments.Any(i => i.PaidOn.HasValue))
                throw new InvalidOperationException("A loan with repayments cannot be cancelled.");
            if (Installments.Any(i => i.AccruedInterest > 0m))
                throw new InvalidOperationException("Interest has been accrued on this loan: reverse the accrual entries first.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A reason is required to cancel a loan.", nameof(reason));

            Status = (int)LoanStatus.Cancelled;
            CancelReason = reason.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>Principal still owed on <paramref name="date"/>: the installments not repaid on or before it, once the loan had started; nothing for a cancelled loan.</summary>
        public decimal OutstandingAt(DateTime date) => InstallmentsOutstandingAt(date).Sum(i => i.PrincipalAmount);

        /// <summary>The part of <see cref="OutstandingAt"/> due later than <paramref name="date"/> + 12 months (long-term).</summary>
        public decimal LongTermAt(DateTime date)
        {
            var limit = date.Date.AddMonths(12);
            return InstallmentsOutstandingAt(date).Where(i => i.DueDate > limit).Sum(i => i.PrincipalAmount);
        }

        public IEnumerable<LoanInstallment> InstallmentsOutstandingAt(DateTime date)
        {
            if (Status == (int)LoanStatus.Cancelled || StartDate > date.Date)
                return Enumerable.Empty<LoanInstallment>();
            return Installments.Where(i => !i.PaidOn.HasValue || i.PaidOn.Value > date.Date);
        }
    }
}

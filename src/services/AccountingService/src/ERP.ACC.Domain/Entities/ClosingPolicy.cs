using ERP.ACC.Domain.Enums;
using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    /// <summary>
    /// How a tenant closes its books (ROADMAP P1-2d). Monthly: every fiscal period writes its own closing entry (the default, the behaviour before P1-2d).
    /// YearEnd: a period only locks, and the last period of the fiscal year (the one ending in <see cref="FiscalYearEndMonth"/>) writes one closing entry
    /// for everything not yet moved to retained earnings. One row per tenant; a tenant with no row is Monthly with a December year end.
    /// </summary>
    public class ClosingPolicy : AggregateRoot
    {
        public int ClosingMode { get; private set; } = (int)Enums.ClosingMode.Monthly;
        public int FiscalYearEndMonth { get; private set; } = 12;

        private ClosingPolicy() { }

        public static ClosingPolicy Create(Guid tenantId, ClosingMode mode, int fiscalYearEndMonth)
        {
            var policy = new ClosingPolicy { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTime.UtcNow };
            policy.Set(mode, fiscalYearEndMonth);
            return policy;
        }

        public void Set(ClosingMode mode, int fiscalYearEndMonth)
        {
            if (!Enum.IsDefined(typeof(ClosingMode), mode))
                throw new ArgumentException("Unknown closing mode.", nameof(mode));
            if (fiscalYearEndMonth is < 1 or > 12)
                throw new ArgumentException("The fiscal year end month must be 1 to 12.", nameof(fiscalYearEndMonth));
            ClosingMode = (int)mode;
            FiscalYearEndMonth = fiscalYearEndMonth;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>True when the period is the last one of a fiscal year: it ends on the last day of the year-end month.</summary>
        public static bool EndsFiscalYear(FiscalPeriod period, int fiscalYearEndMonth) =>
            period.EndDate.Date.Month == fiscalYearEndMonth && period.EndDate.Date.AddDays(1).Month != fiscalYearEndMonth;
    }
}

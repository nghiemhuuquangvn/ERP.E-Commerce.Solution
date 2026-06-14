using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    public class FiscalPeriod : AggregateRoot
    {
        public string PeriodName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Status { get; set; }

        public static FiscalPeriod Create(string periodName, DateTime startDate, DateTime endDate)
        {
            var period = new FiscalPeriod
            {
                Id = Guid.NewGuid(),
                PeriodName = periodName,
                StartDate = startDate,
                EndDate = endDate,
                Status = 0,
                CreatedAt = DateTime.UtcNow
            };

            return period;
        }
    }
}

using ERP.ACC.Domain.Events;
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

        public void Reopen()
        {
            if (Status == 0)
                throw new InvalidOperationException($"Fiscal period {PeriodName} is not closed.");

            Status = 0;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Close()
        {
            if (Status == 1)
                throw new InvalidOperationException($"Fiscal period {PeriodName} is already closed.");

            Status = 1;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new FiscalPeriodClosedEvent(
                Id,
                PeriodName,
                StartDate,
                EndDate,
                DateTime.UtcNow));
        }
    }
}

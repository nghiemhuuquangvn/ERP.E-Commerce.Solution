using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class Holiday : AggregateRoot
    {
        public Guid CalendarId { get; set; }
        public DateTime Date { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsRecurringYearly { get; set; }

        public static Holiday Create(
            Guid calendarId,
            DateTime date,
            string name,
            bool isRecurringYearly = false)
        {
            return new Holiday
            {
                Id = Guid.NewGuid(),
                CalendarId = calendarId,
                Date = date,
                Name = name,
                IsRecurringYearly = isRecurringYearly,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void Update(DateTime date, string name, bool isRecurringYearly)
        {
            Date = date;
            Name = name;
            IsRecurringYearly = isRecurringYearly;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    public class JournalEntry : AggregateRoot
    {
        public string EntryNumber { get; set; } = string.Empty;
        public Guid FiscalPeriodId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }

        public static JournalEntry Create(string entryNumber, Guid fiscalPeriodId, string description)
        {
            var entry = new JournalEntry
            {
                Id = Guid.NewGuid(),
                EntryNumber = entryNumber,
                FiscalPeriodId = fiscalPeriodId,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };

            return entry;
        }
    }
}

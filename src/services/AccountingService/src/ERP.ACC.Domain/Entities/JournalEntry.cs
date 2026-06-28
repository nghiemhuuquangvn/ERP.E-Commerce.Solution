using ERP.ACC.Domain.Events;
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

        public void Post(decimal totalDebit, decimal totalCredit)
        {
            if (totalDebit != totalCredit)
                throw new InvalidOperationException($"Journal entry {EntryNumber} is not balanced.");

            TotalDebit = totalDebit;
            TotalCredit = totalCredit;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new JournalEntryPostedEvent(
                Id,
                EntryNumber,
                FiscalPeriodId,
                Description,
                TotalDebit,
                TotalCredit));
        }
    }
}

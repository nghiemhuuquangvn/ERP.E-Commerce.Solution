using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    public class JournalLine : AggregateRoot
    {
        public Guid JournalEntryId { get; private set; }
        public Guid AccountId { get; private set; }
        public decimal Debit { get; private set; }
        public decimal Credit { get; private set; }
        public new string Description { get; private set; } = string.Empty;

        private JournalLine() { }

        public static JournalLine Create(Guid journalEntryId, Guid accountId, decimal debit, decimal credit, string description = "")
        {
            return new JournalLine
            {
                Id = Guid.NewGuid(),
                JournalEntryId = journalEntryId,
                AccountId = accountId,
                Debit = debit,
                Credit = credit,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}

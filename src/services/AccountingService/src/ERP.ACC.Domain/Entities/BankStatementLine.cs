using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    /// <summary>
    /// One line of a bank statement (ROADMAP P1-4): a movement the bank shows. It is matched to the journal line of the same movement on the bank's ledger account, or posted as a new
    /// entry when the books do not know it yet (a fee, interest). The amount is signed from the account's point of view: a deposit is positive, a withdrawal negative.
    /// </summary>
    public class BankStatementLine : AggregateRoot
    {
        public const int Unmatched = 0;
        public const int Matched = 1;

        /// <summary>The books had no entry for it; one was posted from the line.</summary>
        public const int Posted = 2;

        public Guid BankStatementId { get; private set; }
        public Guid BankAccountId { get; private set; }
        public DateTime LineDate { get; private set; }
        public string? Reference { get; private set; }
        public decimal Amount { get; private set; }
        public decimal? Balance { get; private set; }
        public int MatchStatus { get; private set; }

        /// <summary>The ledger line it was matched to (or the bank line of the entry posted from it).</summary>
        public Guid? MatchedJournalLineId { get; private set; }

        /// <summary>The entry posted from the line (status <see cref="Posted"/>).</summary>
        public Guid? JournalEntryId { get; private set; }

        private BankStatementLine() { }

        public static BankStatementLine Create(Guid bankStatementId, Guid bankAccountId, DateTime lineDate, string? description, string? reference, decimal amount, decimal? balance = null)
        {
            if (amount == 0m)
                throw new ArgumentException("A statement line moves money.", nameof(amount));
            return new BankStatementLine
            {
                Id = Guid.NewGuid(),
                BankStatementId = bankStatementId,
                BankAccountId = bankAccountId,
                LineDate = lineDate.Date,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
                Amount = Math.Round(amount, 2),
                Balance = balance,
                MatchStatus = Unmatched,
                CreatedAt = DateTime.UtcNow,
            };
        }

        public void MatchTo(Guid journalLineId)
        {
            if (MatchStatus != Unmatched)
                throw new InvalidOperationException("The statement line is already matched.");
            MatchedJournalLineId = journalLineId;
            MatchStatus = Matched;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Unmatch()
        {
            if (MatchStatus == Posted)
                throw new InvalidOperationException("The statement line was posted as an entry: reverse that entry instead.");
            if (MatchStatus == Unmatched)
                return;
            MatchedJournalLineId = null;
            MatchStatus = Unmatched;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkPosted(Guid journalEntryId, Guid journalLineId)
        {
            if (MatchStatus != Unmatched)
                throw new InvalidOperationException("The statement line is already matched.");
            JournalEntryId = journalEntryId;
            MatchedJournalLineId = journalLineId;
            MatchStatus = Posted;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

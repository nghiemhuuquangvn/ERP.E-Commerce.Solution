using ERP.ACC.Domain.Enums;
using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    /// <summary>
    /// One movement of an off-balance account (class 0: leased assets, goods held for others, foreign currency, written-off debts …; ROADMAP P1-2i).
    /// It is a register line, not a journal entry: the movement has one side only, so it can never satisfy the double-entry rule, and keeping it
    /// outside the journal keeps every trial balance, closing and statement balanced. <see cref="OffBalanceDirection.In"/> raises the account's
    /// balance, <see cref="OffBalanceDirection.Out"/> lowers it (these accounts have a debit normal balance). The text is <see cref="BaseEntity.Description"/>.
    /// </summary>
    public class OffBalanceEntry : AggregateRoot
    {
        public Guid AccountId { get; private set; }
        public DateTime EntryDate { get; private set; }
        public int Direction { get; private set; }
        public decimal Amount { get; private set; }
        public string? Reference { get; private set; }
        public int Status { get; private set; } = (int)OffBalanceStatus.Posted;
        public string? VoidReason { get; private set; }
        public DateTime? VoidedAt { get; private set; }

        private OffBalanceEntry() { }

        public static OffBalanceEntry Create(Guid accountId, DateTime entryDate, OffBalanceDirection direction, decimal amount, string? description, string? reference)
        {
            if (accountId == Guid.Empty)
                throw new ArgumentException("AccountId is required.", nameof(accountId));
            if (!Enum.IsDefined(typeof(OffBalanceDirection), direction))
                throw new ArgumentException("Unknown direction.", nameof(direction));
            if (amount <= 0m)
                throw new ArgumentException("The amount must be greater than 0.", nameof(amount));

            return new OffBalanceEntry
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                EntryDate = entryDate.Date,
                Direction = (int)direction,
                Amount = Math.Round(amount, 2),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
                CreatedAt = DateTime.UtcNow,
            };
        }

        /// <summary>The signed effect on the account's balance: In adds, Out subtracts; a voided entry has none.</summary>
        public decimal Effect => Status == (int)OffBalanceStatus.Posted ? (Direction == (int)OffBalanceDirection.In ? Amount : -Amount) : 0m;

        public void Void(string reason)
        {
            if (Status != (int)OffBalanceStatus.Posted)
                throw new InvalidOperationException("Only a posted off-balance entry can be voided.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A reason is required to void an off-balance entry.", nameof(reason));

            Status = (int)OffBalanceStatus.Voided;
            VoidReason = reason.Trim();
            VoidedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

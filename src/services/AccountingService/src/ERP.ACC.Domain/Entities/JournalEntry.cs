using ERP.ACC.Domain.Enums;
using ERP.ACC.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    /// <summary>One line of an entry before it exists: exactly one of Debit / Credit is positive.</summary>
    public readonly record struct JournalLineDraft(Guid AccountId, decimal Debit, decimal Credit, string Description = "");

    public class JournalEntry : AggregateRoot
    {
        public string EntryNumber { get; set; } = string.Empty;
        public Guid FiscalPeriodId { get; set; }
        public new string Description { get; set; } = string.Empty;

        /// <summary>Accounting date (a date, not an instant). Reports filter on it.</summary>
        public DateTime EntryDate { get; set; } = DateTime.UtcNow.Date;
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }

        /// <see cref="JournalStatus"/>: Draft 0, Posted 1, Voided 2 (ignored by reports), Reversed 3 (counted; offset by its reversal entry).
        public int Status { get; set; }

        /// <summary>The business document this entry was posted for ("Invoice", "Payment", "Bill", ...), for display and search.</summary>
        public string? ReferenceType { get; set; }
        public Guid? ReferenceId { get; set; }

        /// <summary>
        /// Idempotency key of an automatic posting, "&lt;ReferenceType&gt;:&lt;ReferenceId&gt;": a unique index per tenant means a
        /// redelivered event can never post the same document twice. Manual and legacy entries have none.
        /// </summary>
        public string? SourceKey { get; set; }

        public DateTime? PostedAt { get; set; }
        public string? PostedBy { get; set; }
        public DateTime? VoidedAt { get; set; }
        public string? VoidedBy { get; set; }
        public string? VoidReason { get; set; }

        public List<JournalLine> Lines { get; private set; } = new();

        public static string BuildSourceKey(string referenceType, Guid referenceId) => $"{referenceType}:{referenceId:N}";

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

        public void AddLine(Guid accountId, decimal debit, decimal credit, string description = "")
        {
            Lines.Add(JournalLine.Create(Id, accountId, debit, credit, description));
        }

        /// <summary>Manual posting path (lines were added with <see cref="AddLine"/>).</summary>
        public void Post(decimal totalDebit, decimal totalCredit)
        {
            if (totalDebit != totalCredit)
                throw new InvalidOperationException($"Journal entry {EntryNumber} is not balanced.");

            TotalDebit = totalDebit;
            TotalCredit = totalCredit;
            MarkPosted();
        }

        /// <summary>
        /// An entry generated from a business document, posted at once. The lines are validated here (the database CHECK
        /// would otherwise reject them with a generic error): at least two, amounts rounded to cents, exactly one positive
        /// side per line, debits equal credits.
        /// </summary>
        public static JournalEntry CreatePosted(
            string entryNumber, Guid fiscalPeriodId, DateTime entryDate, string description,
            string referenceType, Guid referenceId, IEnumerable<JournalLineDraft> lines)
        {
            if (string.IsNullOrWhiteSpace(referenceType))
                throw new ArgumentException("ReferenceType is required", nameof(referenceType));
            if (referenceId == Guid.Empty)
                throw new ArgumentException("ReferenceId is required", nameof(referenceId));

            var entry = new JournalEntry
            {
                Id = Guid.NewGuid(),
                EntryNumber = entryNumber,
                FiscalPeriodId = fiscalPeriodId,
                EntryDate = entryDate.Date,
                Description = description.Length > 500 ? description[..500] : description,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                SourceKey = BuildSourceKey(referenceType, referenceId),
                CreatedAt = DateTime.UtcNow
            };

            foreach (var line in lines)
            {
                var debit = Math.Round(line.Debit, 2, MidpointRounding.AwayFromZero);
                var credit = Math.Round(line.Credit, 2, MidpointRounding.AwayFromZero);
                if (debit < 0 || credit < 0 || (debit > 0) == (credit > 0))
                    throw new InvalidOperationException(
                        $"Journal entry {entryNumber}: each line needs exactly one positive amount (debit {debit}, credit {credit}).");

                entry.Lines.Add(JournalLine.Create(entry.Id, line.AccountId, debit, credit, line.Description));
            }

            if (entry.Lines.Count < 2)
                throw new InvalidOperationException($"Journal entry {entryNumber} needs at least two lines.");

            var totalDebit = entry.Lines.Sum(l => l.Debit);
            var totalCredit = entry.Lines.Sum(l => l.Credit);
            if (totalDebit != totalCredit)
                throw new InvalidOperationException($"Journal entry {entryNumber} is not balanced ({totalDebit} / {totalCredit}).");

            entry.TotalDebit = totalDebit;
            entry.TotalCredit = totalCredit;
            entry.MarkPosted();
            return entry;
        }

        /// <summary>ReferenceType of the entries the period-end closing writes (ROADMAP P1-2); they are never voided or reversed by hand: reopen the period instead.</summary>
        public const string PeriodClosingType = "PeriodClosing";

        public bool IsSystemClosingEntry => ReferenceType == PeriodClosingType;

        /// <summary>
        /// Takes a hand-written entry out of the books: status Voided, ignored by every report. Only for a posted entry that no document
        /// owns (no source key, so nothing is waiting to re-post it); the caller checks that its period is still open. An entry that
        /// belongs to a document, or whose period is closed, is reversed instead (a mirror entry keeps the audit trail).
        /// </summary>
        public void Void(string reason, string? voidedBy = null)
        {
            if (Status != (int)JournalStatus.Posted)
                throw new InvalidOperationException($"Journal entry {EntryNumber} is not posted, so it cannot be voided.");
            if (!string.IsNullOrEmpty(SourceKey))
                throw new InvalidOperationException($"Journal entry {EntryNumber} belongs to a document ({ReferenceType}); reverse it instead of voiding it.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A reason is required to void an entry", nameof(reason));

            Status = (int)JournalStatus.Voided;
            VoidedAt = DateTime.UtcNow;
            VoidedBy = voidedBy;
            VoidReason = reason.Length > 500 ? reason[..500] : reason;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Takes a period-closing entry out of the books when its period is reopened. Its source key is renamed so that closing the period
        /// again can post a new one (the old key would otherwise make the engine return the voided entry).
        /// </summary>
        public void VoidForReopen(string reason)
        {
            if (!IsSystemClosingEntry)
                throw new InvalidOperationException($"Journal entry {EntryNumber} is not a period-closing entry.");
            if (Status != (int)JournalStatus.Posted)
                throw new InvalidOperationException($"Journal entry {EntryNumber} is not posted.");

            Status = (int)JournalStatus.Voided;
            VoidedAt = DateTime.UtcNow;
            VoidReason = reason.Length > 500 ? reason[..500] : reason;
            SourceKey = $"{SourceKey}:voided:{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Cancels the effect of this posted entry by a new entry with every line mirrored. The original stays in the books
        /// (status Reversed) so the history is complete and both entries are counted by the reports. The reversal is itself
        /// idempotent: its key is "&lt;ReferenceType&gt;Reversal:&lt;ReferenceId&gt;". <see cref="Lines"/> must be loaded.
        /// </summary>
        public JournalEntry Reverse(string reversalNumber, Guid fiscalPeriodId, DateTime entryDate, string reason)
        {
            if (Status != (int)JournalStatus.Posted)
                throw new InvalidOperationException($"Journal entry {EntryNumber} is not posted, so it cannot be reversed.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A reason is required to reverse an entry", nameof(reason));
            if (Lines.Count == 0)
                throw new InvalidOperationException($"Journal entry {EntryNumber} has no lines loaded.");

            var reversal = CreatePosted(
                reversalNumber, fiscalPeriodId, entryDate,
                $"Reversal of {EntryNumber}: {reason}",
                (ReferenceType ?? "Manual") + "Reversal", ReferenceId ?? Id,
                Lines.Select(l => new JournalLineDraft(l.AccountId, l.Credit, l.Debit, l.Description)));

            Status = (int)JournalStatus.Reversed;
            VoidedAt = DateTime.UtcNow;
            VoidReason = reason.Length > 500 ? reason[..500] : reason;
            UpdatedAt = DateTime.UtcNow;
            return reversal;
        }

        private void MarkPosted()
        {
            Status = (int)JournalStatus.Posted;
            PostedAt = DateTime.UtcNow;
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

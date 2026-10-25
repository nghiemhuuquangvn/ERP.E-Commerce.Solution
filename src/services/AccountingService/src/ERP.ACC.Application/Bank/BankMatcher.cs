using ERP.ACC.Application.DTOs;
using ERP.ACC.Domain.Entities;

namespace ERP.ACC.Application.Bank
{
    /// <summary>Which ledger line a statement line goes with (ROADMAP P1-4).</summary>
    public record BankMatch(Guid StatementLineId, Guid JournalLineId);

    public static class BankMatcher
    {
        public const int DefaultToleranceDays = 3;

        /// <summary>
        /// Pairs statement lines with ledger lines of the same amount (the ledger side is debit − credit on the bank's account: a deposit is a debit) dated within
        /// <paramref name="toleranceDays"/> days. When the statement line carries a reference that appears in the entry number, its description or the line's description,
        /// those candidates win. A line with more than one equally good candidate is left for a person, and a ledger line is used once.
        /// </summary>
        public static List<BankMatch> Match(IEnumerable<BankStatementLine> statementLines, IEnumerable<LedgerRow> ledger, int toleranceDays = DefaultToleranceDays)
        {
            var free = ledger.ToList();
            var matches = new List<BankMatch>();
            // the lines with a reference go first: they are the surest, and take their ledger line before the ambiguous ones look
            foreach (var line in statementLines.Where(l => l.MatchStatus == BankStatementLine.Unmatched).OrderByDescending(l => l.Reference != null).ThenBy(l => l.LineDate))
            {
                var candidates = free
                    .Where(r => r.Debit - r.Credit == line.Amount && Math.Abs((r.EntryDate.Date - line.LineDate.Date).TotalDays) <= toleranceDays)
                    .ToList();
                if (candidates.Count == 0)
                    continue;

                LedgerRow? pick = null;
                if (!string.IsNullOrWhiteSpace(line.Reference))
                {
                    var byReference = candidates.Where(r => Mentions(r, line.Reference!)).ToList();
                    if (byReference.Count == 1)
                        pick = byReference[0];
                    else if (byReference.Count > 1)
                        pick = Closest(byReference, line);
                }
                pick ??= candidates.Count == 1 ? candidates[0] : Closest(candidates, line);
                if (pick == null)
                    continue;

                matches.Add(new BankMatch(line.Id, pick.LineId));
                free.Remove(pick);
            }
            return matches;
        }

        /// <summary>The candidate on exactly the statement line's date when it is the only one; otherwise nothing (ambiguous).</summary>
        private static LedgerRow? Closest(List<LedgerRow> candidates, BankStatementLine line)
        {
            var sameDay = candidates.Where(r => r.EntryDate.Date == line.LineDate.Date).ToList();
            if (sameDay.Count == 1)
                return sameDay[0];
            var nearest = candidates.Min(r => Math.Abs((r.EntryDate.Date - line.LineDate.Date).TotalDays));
            var best = candidates.Where(r => Math.Abs((r.EntryDate.Date - line.LineDate.Date).TotalDays) == nearest).ToList();
            return best.Count == 1 ? best[0] : null;
        }

        private static bool Mentions(LedgerRow row, string reference) =>
            row.EntryNumber.Contains(reference, StringComparison.OrdinalIgnoreCase)
            || row.EntryDescription.Contains(reference, StringComparison.OrdinalIgnoreCase)
            || row.LineDescription.Contains(reference, StringComparison.OrdinalIgnoreCase);
    }
}

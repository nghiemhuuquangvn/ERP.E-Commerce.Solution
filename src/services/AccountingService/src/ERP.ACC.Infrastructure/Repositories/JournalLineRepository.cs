using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class JournalLineRepository : IJournalLineRepository
    {
        private readonly AccountingDbContext _db;

        public JournalLineRepository(AccountingDbContext db) => _db = db;

        public async Task<List<JournalLine>> GetByJournalEntryIdAsync(Guid journalEntryId, CancellationToken ct = default) =>
            await _db.JournalLines
                .Where(l => l.JournalEntryId == journalEntryId)
                .AsNoTracking()
                .ToListAsync(ct);

        public async Task<List<AccountBalanceSummary>> GetAccountBalancesAsync(
            DateTime? fromDate,
            DateTime? toDate,
            Guid? fiscalPeriodId,
            CancellationToken ct = default)
        {
            // Only entries that are in the books: Posted, and Reversed (their reversal entry offsets them). Drafts and voided
            // entries are not part of any balance.
            var query = _db.JournalLines
                .Join(_db.JournalEntries.Where(e => e.Status == 1 || e.Status == 3),
                    line => line.JournalEntryId,
                    entry => entry.Id,
                    (line, entry) => new { Line = line, Entry = entry });

            if (fiscalPeriodId.HasValue)
                query = query.Where(x => x.Entry.FiscalPeriodId == fiscalPeriodId.Value);

            if (fromDate.HasValue)
                query = query.Where(x => x.Entry.EntryDate >= fromDate.Value.Date);

            if (toDate.HasValue)
                query = query.Where(x => x.Entry.EntryDate <= toDate.Value.Date);

            return await query
                .GroupBy(x => x.Line.AccountId)
                .Select(g => new AccountBalanceSummary
                {
                    AccountId = g.Key,
                    TotalDebit = g.Sum(x => x.Line.Debit),
                    TotalCredit = g.Sum(x => x.Line.Credit)
                })
                .ToListAsync(ct);
        }

        public async Task<List<AccountBalanceSummary>> GetActivityBalancesAsync(
            DateTime? fromDate,
            DateTime? toDate,
            string? excludeReferenceType,
            CancellationToken ct = default)
        {
            var entries = _db.JournalEntries.Where(e => e.Status == 1 || e.Status == 3);
            if (excludeReferenceType != null)
                entries = entries.Where(e => e.ReferenceType != excludeReferenceType || e.ReferenceType == null);

            var query = _db.JournalLines.Join(entries, line => line.JournalEntryId, entry => entry.Id, (line, entry) => new { Line = line, Entry = entry });

            if (fromDate.HasValue)
                query = query.Where(x => x.Entry.EntryDate >= fromDate.Value.Date);
            if (toDate.HasValue)
                query = query.Where(x => x.Entry.EntryDate <= toDate.Value.Date);

            return await query
                .GroupBy(x => x.Line.AccountId)
                .Select(g => new AccountBalanceSummary { AccountId = g.Key, TotalDebit = g.Sum(x => x.Line.Debit), TotalCredit = g.Sum(x => x.Line.Credit) })
                .ToListAsync(ct);
        }

        public async Task<List<LedgerRow>> GetLedgerLinesAsync(
            IReadOnlyCollection<Guid> accountIds,
            DateTime? fromDate,
            DateTime? toDate,
            CancellationToken ct = default)
        {
            var ids = accountIds.ToList();
            var query = _db.JournalLines
                .Where(l => ids.Contains(l.AccountId))
                .Join(_db.JournalEntries.Where(e => e.Status == 1 || e.Status == 3),
                    line => line.JournalEntryId, entry => entry.Id, (line, entry) => new { Line = line, Entry = entry });

            if (fromDate.HasValue)
                query = query.Where(x => x.Entry.EntryDate >= fromDate.Value.Date);
            if (toDate.HasValue)
                query = query.Where(x => x.Entry.EntryDate <= toDate.Value.Date);

            return await query
                .OrderBy(x => x.Entry.EntryDate).ThenBy(x => x.Entry.EntryNumber).ThenBy(x => x.Line.CreatedAt)
                .Select(x => new LedgerRow
                {
                    EntryId = x.Entry.Id,
                    EntryNumber = x.Entry.EntryNumber,
                    EntryDate = x.Entry.EntryDate,
                    EntryDescription = x.Entry.Description,
                    LineDescription = x.Line.Description,
                    ReferenceType = x.Entry.ReferenceType,
                    Status = x.Entry.Status,
                    AccountId = x.Line.AccountId,
                    Debit = x.Line.Debit,
                    Credit = x.Line.Credit,
                })
                .AsNoTracking()
                .ToListAsync(ct);
        }

        public async Task<List<EntryAccountRef>> GetEntryAccountsAsync(IReadOnlyCollection<Guid> entryIds, CancellationToken ct = default)
        {
            var ids = entryIds.ToList();
            if (ids.Count == 0)
                return new List<EntryAccountRef>();

            return await _db.JournalLines
                .Where(l => ids.Contains(l.JournalEntryId))
                .Select(l => new EntryAccountRef(l.JournalEntryId, l.AccountId))
                .AsNoTracking()
                .ToListAsync(ct);
        }

        public async Task AddRangeAsync(IEnumerable<JournalLine> lines, CancellationToken ct = default)
        {
            await _db.JournalLines.AddRangeAsync(lines, ct);
        }
    }
}

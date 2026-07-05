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
            var query = _db.JournalLines
                .Join(_db.JournalEntries,
                    line => line.JournalEntryId,
                    entry => entry.Id,
                    (line, entry) => new { Line = line, Entry = entry });

            if (fiscalPeriodId.HasValue)
                query = query.Where(x => x.Entry.FiscalPeriodId == fiscalPeriodId.Value);

            if (fromDate.HasValue)
                query = query.Where(x => x.Entry.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(x => x.Entry.CreatedAt <= toDate.Value);

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

        public async Task AddRangeAsync(IEnumerable<JournalLine> lines, CancellationToken ct = default)
        {
            await _db.JournalLines.AddRangeAsync(lines, ct);
        }
    }
}

using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class JournalEntryRepository : IJournalEntryRepository
    {
        private readonly AccountingDbContext _db;

        public JournalEntryRepository(AccountingDbContext db) => _db = db;

        public async Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            await _db.JournalEntries.Include(j => j.Lines).FirstOrDefaultAsync(j => j.Id == id, ct);

        public async Task<List<JournalEntry>> GetAllAsync(CancellationToken ct = default) =>
            await _db.JournalEntries.Include(j => j.Lines).OrderByDescending(j => j.CreatedAt).AsNoTracking().ToListAsync(ct);

        public async Task<List<JournalEntry>> GetByFiscalPeriodIdAsync(Guid fiscalPeriodId, CancellationToken ct = default) =>
            await _db.JournalEntries.Include(j => j.Lines).Where(j => j.FiscalPeriodId == fiscalPeriodId).OrderByDescending(j => j.CreatedAt).AsNoTracking().ToListAsync(ct);

        public async Task<bool> AnyAsync(CancellationToken ct = default) =>
            await _db.JournalEntries.AnyAsync(ct);

        public async Task<JournalEntry?> GetBySourceKeyAsync(string sourceKey, CancellationToken ct = default) =>
            await _db.JournalEntries.Include(j => j.Lines).FirstOrDefaultAsync(j => j.SourceKey == sourceKey, ct);

        public async Task AddAsync(JournalEntry entry, CancellationToken ct = default)
        {
            await _db.JournalEntries.AddAsync(entry, ct);
        }

        public Task UpdateAsync(JournalEntry entry, CancellationToken ct = default)
        {
            _db.JournalEntries.Update(entry);
            return Task.CompletedTask;
        }
    }
}

using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class OffBalanceRepository : IOffBalanceRepository
    {
        private readonly AccountingDbContext _db;

        public OffBalanceRepository(AccountingDbContext db) => _db = db;

        public async Task<OffBalanceEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            await _db.OffBalanceEntries.FirstOrDefaultAsync(e => e.Id == id, ct);

        public async Task<List<OffBalanceEntry>> ListAsync(Guid? accountId, DateTime? from, DateTime? to, CancellationToken ct = default)
        {
            var query = _db.OffBalanceEntries.AsNoTracking().AsQueryable();
            if (accountId.HasValue)
                query = query.Where(e => e.AccountId == accountId.Value);
            if (from.HasValue)
                query = query.Where(e => e.EntryDate >= from.Value.Date);
            if (to.HasValue)
                query = query.Where(e => e.EntryDate <= to.Value.Date);
            return await query.OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.CreatedAt).ToListAsync(ct);
        }

        public async Task AddAsync(OffBalanceEntry entry, CancellationToken ct = default) =>
            await _db.OffBalanceEntries.AddAsync(entry, ct);
    }
}

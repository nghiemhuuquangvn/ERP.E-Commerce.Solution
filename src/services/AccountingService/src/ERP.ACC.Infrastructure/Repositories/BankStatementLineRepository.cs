using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class BankStatementLineRepository : IBankStatementLineRepository
    {
        private readonly AccountingDbContext _db;

        public BankStatementLineRepository(AccountingDbContext db) => _db = db;

        public async Task<BankStatementLine?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            await _db.BankStatementLines.FirstOrDefaultAsync(l => l.Id == id, ct);

        public async Task<List<BankStatementLine>> ListByStatementAsync(Guid bankStatementId, CancellationToken ct = default) =>
            await _db.BankStatementLines.Where(l => l.BankStatementId == bankStatementId).OrderBy(l => l.LineDate).ThenBy(l => l.CreatedAt).ToListAsync(ct);

        public async Task<List<BankStatementLine>> ListByBankAccountAsync(Guid bankAccountId, DateTime to, CancellationToken ct = default) =>
            await _db.BankStatementLines.AsNoTracking().Where(l => l.BankAccountId == bankAccountId && l.LineDate <= to.Date).ToListAsync(ct);

        public async Task<HashSet<Guid>> MatchedJournalLineIdsAsync(CancellationToken ct = default) =>
            (await _db.BankStatementLines.AsNoTracking().Where(l => l.MatchedJournalLineId != null).Select(l => l.MatchedJournalLineId!.Value).ToListAsync(ct)).ToHashSet();

        public async Task AddRangeAsync(IEnumerable<BankStatementLine> lines, CancellationToken ct = default) =>
            await _db.BankStatementLines.AddRangeAsync(lines, ct);

        public void Remove(BankStatementLine line) => _db.BankStatementLines.Remove(line);
    }
}

using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class BankStatementRepository : IBankStatementRepository
    {
        private readonly AccountingDbContext _db;

        public BankStatementRepository(AccountingDbContext db) => _db = db;

        public async Task<BankStatement?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            await _db.BankStatements.FirstOrDefaultAsync(s => s.Id == id, ct);

        public async Task<List<BankStatement>> GetByBankAccountIdAsync(Guid bankAccountId, CancellationToken ct = default) =>
            await _db.BankStatements
                .Where(s => s.BankAccountId == bankAccountId)
                .OrderByDescending(s => s.StatementDate)
                .AsNoTracking()
                .ToListAsync(ct);

        public async Task AddAsync(BankStatement statement, CancellationToken ct = default)
        {
            await _db.BankStatements.AddAsync(statement, ct);
            await _db.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(BankStatement statement, CancellationToken ct = default)
        {
            _db.BankStatements.Update(statement);
            await _db.SaveChangesAsync(ct);
        }
    }
}

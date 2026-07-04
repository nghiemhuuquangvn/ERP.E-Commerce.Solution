using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class BankAccountRepository : IBankAccountRepository
    {
        private readonly AccountingDbContext _db;

        public BankAccountRepository(AccountingDbContext db) => _db = db;

        public async Task<BankAccount?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            await _db.BankAccounts.FirstOrDefaultAsync(b => b.Id == id, ct);

        public async Task<List<BankAccount>> GetAllAsync(bool? activeOnly = null, CancellationToken ct = default)
        {
            var query = _db.BankAccounts.AsQueryable();
            if (activeOnly.HasValue)
                query = query.Where(b => b.IsActive == activeOnly.Value);
            return await query.OrderBy(b => b.AccountName).AsNoTracking().ToListAsync(ct);
        }

        public async Task AddAsync(BankAccount bankAccount, CancellationToken ct = default)
        {
            await _db.BankAccounts.AddAsync(bankAccount, ct);
            await _db.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(BankAccount bankAccount, CancellationToken ct = default)
        {
            _db.BankAccounts.Update(bankAccount);
            await _db.SaveChangesAsync(ct);
        }
    }
}

using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly AccountingDbContext _db;

        public AccountRepository(AccountingDbContext db) => _db = db;

        public async Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            await _db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);

        public async Task<List<Account>> GetAllAsync(CancellationToken ct = default) =>
            await _db.Accounts.OrderBy(a => a.AccountNumber).AsNoTracking().ToListAsync(ct);

        public async Task AddAsync(Account account, CancellationToken ct = default)
        {
            await _db.Accounts.AddAsync(account, ct);
        }

        public Task UpdateAsync(Account account, CancellationToken ct = default)
        {
            _db.Accounts.Update(account);
            return Task.CompletedTask;
        }
    }
}

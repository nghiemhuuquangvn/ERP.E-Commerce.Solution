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

        public async Task<List<Account>> GetAllTrackedAsync(CancellationToken ct = default) =>
            await _db.Accounts.OrderBy(a => a.AccountNumber).ToListAsync(ct);

        public async Task<HashSet<Guid>> GetParentAccountIdsAsync(CancellationToken ct = default) =>
            (await _db.Accounts.AsNoTracking()
                .Where(a => a.IsActive && a.ParentAccountId != null)
                .Select(a => a.ParentAccountId!.Value)
                .Distinct()
                .ToListAsync(ct)).ToHashSet();

        public async Task<List<Account>> GetByNumbersAsync(IEnumerable<string> accountNumbers, CancellationToken ct = default)
        {
            var numbers = accountNumbers.ToList();
            return await _db.Accounts.Where(a => numbers.Contains(a.AccountNumber)).ToListAsync(ct);
        }

        public async Task<List<Account>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        {
            var list = ids.ToList();
            return await _db.Accounts.Where(a => list.Contains(a.Id)).ToListAsync(ct);
        }

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

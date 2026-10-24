using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class ClosingPolicyRepository : IClosingPolicyRepository
    {
        private readonly AccountingDbContext _db;

        public ClosingPolicyRepository(AccountingDbContext db) => _db = db;

        public async Task<ClosingPolicy?> GetAsync(CancellationToken ct = default) =>
            await _db.ClosingPolicies.FirstOrDefaultAsync(ct);

        public async Task AddAsync(ClosingPolicy policy, CancellationToken ct = default) =>
            await _db.ClosingPolicies.AddAsync(policy, ct);
    }
}

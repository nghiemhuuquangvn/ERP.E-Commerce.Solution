using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class StatementMappingRepository : IStatementMappingRepository
    {
        private readonly AccountingDbContext _db;

        public StatementMappingRepository(AccountingDbContext db) => _db = db;

        public async Task<List<StatementAccountMapping>> GetAllAsync(CancellationToken ct = default) =>
            await _db.StatementAccountMappings.OrderBy(m => m.StatementId).ThenBy(m => m.RowCode).ThenBy(m => m.AccountPrefix).ToListAsync(ct);

        public async Task AddAsync(StatementAccountMapping mapping, CancellationToken ct = default) =>
            await _db.StatementAccountMappings.AddAsync(mapping, ct);

        public void Remove(StatementAccountMapping mapping) => _db.StatementAccountMappings.Remove(mapping);
    }
}

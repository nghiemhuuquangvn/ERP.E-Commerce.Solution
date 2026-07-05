using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class TaxRateRepository : ITaxRateRepository
    {
        private readonly AccountingDbContext _db;

        public TaxRateRepository(AccountingDbContext db) => _db = db;

        public async Task<TaxRate?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            await _db.TaxRates.FirstOrDefaultAsync(t => t.Id == id, ct);

        public async Task<List<TaxRate>> GetAllAsync(bool? activeOnly = null, CancellationToken ct = default)
        {
            var query = _db.TaxRates.AsQueryable();
            if (activeOnly.HasValue)
                query = query.Where(t => t.IsActive == activeOnly.Value);
            return await query.OrderBy(t => t.Code).AsNoTracking().ToListAsync(ct);
        }

        public async Task AddAsync(TaxRate taxRate, CancellationToken ct = default)
        {
            await _db.TaxRates.AddAsync(taxRate, ct);
        }

        public Task UpdateAsync(TaxRate taxRate, CancellationToken ct = default)
        {
            _db.TaxRates.Update(taxRate);
            return Task.CompletedTask;
        }
    }
}

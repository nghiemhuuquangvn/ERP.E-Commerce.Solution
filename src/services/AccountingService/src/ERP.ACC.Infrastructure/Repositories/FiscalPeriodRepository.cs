using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class FiscalPeriodRepository : IFiscalPeriodRepository
    {
        private readonly AccountingDbContext _db;

        public FiscalPeriodRepository(AccountingDbContext db) => _db = db;

        public async Task<FiscalPeriod?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            await _db.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == id, ct);

        public async Task<List<FiscalPeriod>> GetAllAsync(CancellationToken ct = default) =>
            await _db.FiscalPeriods.OrderByDescending(p => p.StartDate).AsNoTracking().ToListAsync(ct);

        public async Task<FiscalPeriod?> GetOpenPeriodAsync(CancellationToken ct = default) =>
            await _db.FiscalPeriods.Where(p => p.Status == 0).OrderByDescending(p => p.StartDate).FirstOrDefaultAsync(ct);

        public async Task AddAsync(FiscalPeriod period, CancellationToken ct = default)
        {
            await _db.FiscalPeriods.AddAsync(period, ct);
            await _db.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(FiscalPeriod period, CancellationToken ct = default)
        {
            _db.FiscalPeriods.Update(period);
            await _db.SaveChangesAsync(ct);
        }
    }
}

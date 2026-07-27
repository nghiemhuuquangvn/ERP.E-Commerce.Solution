using ERP.SO.Application.Interfaces;
using ERP.SO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.SO.Infrastructure.Repositories
{
    public class CommissionPlanRepository : ICommissionPlanRepository
    {
        private readonly SalesOrderDbContext _context;

        public CommissionPlanRepository(SalesOrderDbContext context)
        {
            _context = context;
        }

        public async Task<CommissionPlan?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await _context.CommissionPlans.FirstOrDefaultAsync(p => p.Id == id, ct);

        public async Task<CommissionPlan?> GetFirstActiveAsync(CancellationToken ct = default)
            => await _context.CommissionPlans.FirstOrDefaultAsync(p => p.IsActive, ct);

        public async Task<List<CommissionPlan>> GetAllAsync(CancellationToken ct = default)
            => await _context.CommissionPlans.AsNoTracking().ToListAsync(ct);

        public async Task AddAsync(CommissionPlan plan, CancellationToken ct = default)
        {
            await _context.CommissionPlans.AddAsync(plan, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(CommissionPlan plan, CancellationToken ct = default)
        {
            _context.CommissionPlans.Update(plan);
            await _context.SaveChangesAsync(ct);
        }
    }
}

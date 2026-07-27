using ERP.SO.Application.Interfaces;
using ERP.SO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.SO.Infrastructure.Repositories
{
    public class CommissionRecordRepository : ICommissionRecordRepository
    {
        private readonly SalesOrderDbContext _context;

        public CommissionRecordRepository(SalesOrderDbContext context)
        {
            _context = context;
        }

        public async Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken ct = default)
            => await _context.CommissionRecords.AnyAsync(r => r.OrderId == orderId, ct);

        public async Task AddAsync(CommissionRecord record, CancellationToken ct = default)
        {
            await _context.CommissionRecords.AddAsync(record, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<List<CommissionRecord>> ListAsync(Guid? salesRepId, DateTime? from, DateTime? to, CancellationToken ct = default)
        {
            var query = _context.CommissionRecords.AsNoTracking().AsQueryable();

            if (salesRepId.HasValue)
                query = query.Where(r => r.SalesRepId == salesRepId.Value);
            if (from.HasValue)
                query = query.Where(r => r.CalculatedAt >= from.Value);
            if (to.HasValue)
                query = query.Where(r => r.CalculatedAt <= to.Value);

            return await query.OrderByDescending(r => r.CalculatedAt).ToListAsync(ct);
        }
    }
}

using ERP.SO.Application.Interfaces;
using ERP.SO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.SO.Infrastructure.Repositories
{
    public class SalesQuotationRepository : ISalesQuotationRepository
    {
        private readonly SalesOrderDbContext _context;

        public SalesQuotationRepository(SalesOrderDbContext context)
        {
            _context = context;
        }

        public async Task<SalesQuotation?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await _context.SalesQuotations.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id, ct);

        public async Task AddAsync(SalesQuotation quotation, CancellationToken ct = default)
        {
            await _context.SalesQuotations.AddAsync(quotation, ct);
            DomainEventOutboxWriter.Stage(_context, quotation);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(SalesQuotation quotation, CancellationToken ct = default)
        {
            _context.SalesQuotations.Update(quotation);
            DomainEventOutboxWriter.Stage(_context, quotation);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<List<SalesQuotation>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
            => await _context.SalesQuotations.AsNoTracking()
                .Where(q => q.CustomerId == customerId)
                .OrderByDescending(q => q.Id)
                .ToListAsync(ct);

        public async Task<List<SalesQuotation>> GetAllAsync(int skip = 0, int take = 20, CancellationToken ct = default)
            => await _context.SalesQuotations.AsNoTracking()
                .OrderByDescending(q => q.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);

        public async Task<int> GetTotalCountAsync(Guid? customerId = null, CancellationToken ct = default)
        {
            var query = _context.SalesQuotations.AsNoTracking();
            if (customerId.HasValue)
                query = query.Where(q => q.CustomerId == customerId);
            return await query.CountAsync(ct);
        }
    }
}

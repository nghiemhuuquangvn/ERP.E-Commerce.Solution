using ERP.SO.Application.Interfaces;
using ERP.SO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.SO.Infrastructure.Repositories
{
    public class CustomerSalesProfileRepository : ICustomerSalesProfileRepository
    {
        private readonly SalesOrderDbContext _context;

        public CustomerSalesProfileRepository(SalesOrderDbContext context)
        {
            _context = context;
        }

        public async Task<CustomerSalesProfile?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
        {
            return await _context.CustomerSalesProfiles
                .FirstOrDefaultAsync(p => p.CustomerId == customerId, ct);
        }

        public async Task AddAsync(CustomerSalesProfile profile, CancellationToken ct = default)
        {
            await _context.CustomerSalesProfiles.AddAsync(profile, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(CustomerSalesProfile profile, CancellationToken ct = default)
        {
            _context.CustomerSalesProfiles.Update(profile);
            await _context.SaveChangesAsync(ct);
        }
    }
}

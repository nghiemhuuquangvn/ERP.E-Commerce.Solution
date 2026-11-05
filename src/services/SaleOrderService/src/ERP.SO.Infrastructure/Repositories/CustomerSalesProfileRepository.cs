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

        public async Task<(List<CreditProfileRow> Rows, int Total)> ListAsync(CreditProfileSearch search, int skip, int take, CancellationToken ct = default)
        {
            var profiles = _context.CustomerSalesProfiles.AsNoTracking().AsQueryable();
            if (search.CustomerId.HasValue)
            {
                var id = search.CustomerId.Value;
                profiles = profiles.Where(p => p.CustomerId == id);
            }
            else if (search.Text != null)
            {
                var text = search.Text;
                var matching = _context.SalesOrders.Where(o => o.CustomerName.ToLower().Contains(text)).Select(o => o.CustomerId).Distinct();
                profiles = profiles.Where(p => matching.Contains(p.CustomerId));
            }

            var total = await profiles.CountAsync(ct);
            var page = await profiles.OrderByDescending(p => p.CurrentBalance).ThenBy(p => p.CustomerId).Skip(skip).Take(take).ToListAsync(ct);

            var ids = page.Select(p => p.CustomerId).ToList();
            var names = await _context.SalesOrders.AsNoTracking()
                .Where(o => ids.Contains(o.CustomerId))
                .GroupBy(o => o.CustomerId)
                .Select(g => new { Id = g.Key, Name = g.OrderByDescending(o => o.OrderDate).Select(o => o.CustomerName).FirstOrDefault() })
                .ToListAsync(ct);
            var nameOf = names.ToDictionary(n => n.Id, n => n.Name ?? string.Empty);

            return (page.Select(p => new CreditProfileRow(p.CustomerId, nameOf.GetValueOrDefault(p.CustomerId, string.Empty), p.CreditLimit, p.CurrentBalance, p.UpdatedAt)).ToList(), total);
        }

        public async Task<string> GetCustomerNameAsync(Guid customerId, CancellationToken ct = default)
        {
            return await _context.SalesOrders.AsNoTracking()
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => o.CustomerName)
                .FirstOrDefaultAsync(ct) ?? string.Empty;
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

using ERP.SO.Application.Handlers;
using ERP.SO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.SO.Infrastructure.Repositories
{
    public class SalesOrderRepository : ISalesOrderRepository
    {
        private readonly SalesOrderDbContext _context;

        public SalesOrderRepository(SalesOrderDbContext context)
        {
            _context = context;
        }

        public async Task<SalesOrder?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _context.SalesOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id, ct);
        }

        public async Task AddAsync(SalesOrder order, CancellationToken ct = default)
        {
            await _context.SalesOrders.AddAsync(order, ct);
        }

        public Task UpdateAsync(SalesOrder order, CancellationToken ct = default)
        {
            _context.SalesOrders.Update(order);
            return Task.CompletedTask;
        }

        public async Task<List<SalesOrder>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
        {
            return await _context.SalesOrders
                .AsNoTracking()
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync(ct);
        }

        public async Task<List<SalesOrder>> GetAllAsync(int skip = 0, int take = 20, CancellationToken ct = default)
        {
            return await _context.SalesOrders
                .AsNoTracking()
                .OrderByDescending(o => o.OrderDate)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);
        }

        public async Task<int> GetTotalCountAsync(Guid? customerId = null, OrderStatus? status = null, CancellationToken ct = default)
        {
            var query = _context.SalesOrders.AsNoTracking();

            if (customerId.HasValue)
            {
                query = query.Where(o => o.CustomerId == customerId);
            }

            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status);
            }

            return await query.CountAsync(ct);
        }

        public async Task<List<SalesOrder>> GetByStatusAsync(OrderStatus status, CancellationToken ct = default)
        {
            return await _context.SalesOrders
                .AsNoTracking()
                .Where(o => o.Status == status)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync(ct);
        }

        /// <summary>Orders that still have this product on a not-yet-fulfilled line — used by ProductPriceChangedConsumer to flag a price-change warning.</summary>
        public async Task<List<SalesOrder>> GetOpenOrdersContainingProductAsync(Guid productId, CancellationToken ct = default)
        {
            var openStatuses = new[] { OrderStatus.Draft, OrderStatus.Submitted, OrderStatus.Approved, OrderStatus.Picked };

            return await _context.SalesOrders
                .AsNoTracking()
                .Where(o => openStatuses.Contains(o.Status) && o.LineItems.Any(li => li.ProductId == productId))
                .ToListAsync(ct);
        }
    }
}

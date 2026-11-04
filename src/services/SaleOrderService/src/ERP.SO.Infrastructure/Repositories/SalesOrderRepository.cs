using ERP.SO.Application.Handlers;
using ERP.SO.Application.Queries;
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
            // Tracked on purpose: handlers load the order, change it and call UpdateAsync. The line items are an
            // owned collection with a shadow key, whose value EF cannot keep for an untracked entity ("The value of
            // shadow key property 'OrderLineItem.Id' is unknown when attempting to save changes"), so submit,
            // approve, cancel ... all failed. The list/query methods below stay AsNoTracking.
            return await _context.SalesOrders
                .FirstOrDefaultAsync(o => o.Id == id, ct);
        }

        public async Task AddAsync(SalesOrder order, CancellationToken ct = default)
        {
            await _context.SalesOrders.AddAsync(order, ct);
            DomainEventOutboxWriter.Stage(_context, order);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(SalesOrder order, CancellationToken ct = default)
        {
            _context.SalesOrders.Update(order);
            DomainEventOutboxWriter.Stage(_context, order);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<List<SalesOrder>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
        {
            return await _context.SalesOrders
                .AsNoTracking()
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync(ct);
        }

        public async Task<List<SalesOrder>> GetAllAsync(int skip, int take, OrderListFilter filter, CancellationToken ct = default)
        {
            return await filter.Apply(_context.SalesOrders.AsNoTracking())
                .OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.OrderNo)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);
        }

        public async Task<int> GetTotalCountAsync(OrderListFilter filter, CancellationToken ct = default)
        {
            return await filter.Apply(_context.SalesOrders.AsNoTracking()).CountAsync(ct);
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

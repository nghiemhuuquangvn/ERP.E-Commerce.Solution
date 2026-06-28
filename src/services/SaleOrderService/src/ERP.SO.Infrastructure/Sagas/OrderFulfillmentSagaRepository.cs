using ERP.SO.Application.Sagas;
using ERP.SO.Domain.Sagas;
using Microsoft.EntityFrameworkCore;

namespace ERP.SO.Infrastructure.Sagas;

public sealed class OrderFulfillmentSagaRepository : IOrderFulfillmentSagaRepository
{
    private readonly SalesOrderDbContext _dbContext;

    public OrderFulfillmentSagaRepository(SalesOrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OrderFulfillmentSagaState?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await _dbContext.Set<OrderFulfillmentSagaState>()
            .FirstOrDefaultAsync(s => s.OrderId == orderId, ct);

    public async Task<OrderFulfillmentSagaState?> GetByIdAsync(Guid sagaId, CancellationToken ct = default)
        => await _dbContext.Set<OrderFulfillmentSagaState>()
            .FindAsync([sagaId], ct);

    public async Task AddAsync(OrderFulfillmentSagaState state, CancellationToken ct = default)
    {
        await _dbContext.Set<OrderFulfillmentSagaState>().AddAsync(state, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(OrderFulfillmentSagaState state, CancellationToken ct = default)
    {
        _dbContext.Set<OrderFulfillmentSagaState>().Update(state);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<OrderFulfillmentSagaState>> GetStuckSagasAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - timeout;
        return await _dbContext.Set<OrderFulfillmentSagaState>()
            .Where(s => s.Status == SagaStatus.Running && s.LastUpdatedAt < cutoff)
            .ToListAsync(ct);
    }
}

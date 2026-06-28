using ERP.SO.Domain.Sagas;

namespace ERP.SO.Application.Sagas;

public interface IOrderFulfillmentSagaRepository
{
    Task<OrderFulfillmentSagaState?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<OrderFulfillmentSagaState?> GetByIdAsync(Guid sagaId, CancellationToken ct = default);
    Task AddAsync(OrderFulfillmentSagaState state, CancellationToken ct = default);
    Task UpdateAsync(OrderFulfillmentSagaState state, CancellationToken ct = default);
    Task<IEnumerable<OrderFulfillmentSagaState>> GetStuckSagasAsync(TimeSpan timeout, CancellationToken ct = default);
}

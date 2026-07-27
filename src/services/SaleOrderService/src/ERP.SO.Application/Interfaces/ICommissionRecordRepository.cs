using ERP.SO.Domain.Entities;

namespace ERP.SO.Application.Interfaces;

public interface ICommissionRecordRepository
{
    Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken ct = default);
    Task AddAsync(CommissionRecord record, CancellationToken ct = default);
    Task<List<CommissionRecord>> ListAsync(Guid? salesRepId, DateTime? from, DateTime? to, CancellationToken ct = default);
}

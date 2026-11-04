using ERP.SO.Domain.Entities;

namespace ERP.SO.Application.Interfaces;

public interface ICommissionRecordRepository
{
    Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken ct = default);
    Task AddAsync(CommissionRecord record, CancellationToken ct = default);
    /// <summary>One page (newest calculation first) and how many records match in all.</summary>
    Task<(List<CommissionRecord> Items, int Total)> ListAsync(Guid? salesRepId, DateTime? from, DateTime? to, int skip, int take, CancellationToken ct = default);
}

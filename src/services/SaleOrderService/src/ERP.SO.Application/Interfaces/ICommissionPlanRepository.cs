using ERP.SO.Domain.Entities;

namespace ERP.SO.Application.Interfaces;

public interface ICommissionPlanRepository
{
    Task<CommissionPlan?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CommissionPlan?> GetFirstActiveAsync(CancellationToken ct = default);
    Task<List<CommissionPlan>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(CommissionPlan plan, CancellationToken ct = default);
    Task UpdateAsync(CommissionPlan plan, CancellationToken ct = default);
}

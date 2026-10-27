using ERP.SHP.Application.DTOs;

namespace ERP.SHP.Application.Interfaces;

public interface ICarrierRuleService
{
    Task<IReadOnlyList<CarrierRuleDto>> ListAsync(CancellationToken ct);
    Task<CarrierRuleDto> CreateAsync(SaveCarrierRuleRequest request, CancellationToken ct);
    Task<CarrierRuleDto> UpdateAsync(Guid id, SaveCarrierRuleRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

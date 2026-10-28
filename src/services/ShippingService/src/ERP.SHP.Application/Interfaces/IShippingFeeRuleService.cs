using ERP.SHP.Application.DTOs;

namespace ERP.SHP.Application.Interfaces;

/// <summary>The shop's delivery price list (ROADMAP P1-11c). Invalid input is a business-rule error, HTTP 400.</summary>
public interface IShippingFeeRuleService
{
    Task<IReadOnlyList<ShippingFeeRuleDto>> ListAsync(CancellationToken ct);
    Task<ShippingFeeRuleDto> CreateAsync(SaveShippingFeeRuleRequest request, CancellationToken ct);
    Task<ShippingFeeRuleDto> UpdateAsync(Guid id, SaveShippingFeeRuleRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

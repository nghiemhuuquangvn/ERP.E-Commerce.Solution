using ERP.Identity.Application.DTOs.Tenant;

namespace ERP.Identity.Application.Interfaces.Services;

public interface ITenantService
{
    Task<IReadOnlyList<TenantDto>> ListAllAsync(CancellationToken ct);
    Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken ct);
    Task UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken ct);
    Task ActivateAsync(Guid id, CancellationToken ct);
    Task DeactivateAsync(Guid id, CancellationToken ct);
}

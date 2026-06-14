using ERP.Identity.Application.DTOs.Permission;

namespace ERP.Identity.Application.Interfaces.Services;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, IEnumerable<string> permissionCodes);
    Task<IReadOnlyList<PermissionDto>> ListAllAsync(CancellationToken ct);
    Task<PermissionDto> CreateAsync(CreatePermissionRequest request, CancellationToken ct);
    Task UpdateAsync(Guid id, UpdatePermissionRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
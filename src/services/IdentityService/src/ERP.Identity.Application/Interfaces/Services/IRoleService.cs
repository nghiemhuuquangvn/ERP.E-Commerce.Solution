using ERP.Identity.Application.DTOs.Permission;

namespace ERP.Identity.Application.Interfaces.Services;

public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct);
    Task<RoleDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<RoleDto> CreateAsync(Guid tenantId, CreateRoleRequest request, CancellationToken ct);
    Task UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task AssignPermissionAsync(Guid roleId, string permissionCode, Guid tenantId, CancellationToken ct);
    Task RemovePermissionAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct);
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid roleId, CancellationToken ct);
}

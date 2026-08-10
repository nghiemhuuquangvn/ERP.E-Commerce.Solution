using ERP.Identity.Application.DTOs.Permission;

namespace ERP.Identity.Application.Interfaces.Services;

public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct);

    /// <summary>Tenant-boundary-enforced — null if the role doesn't exist or belongs
    /// to a different tenant.</summary>
    Task<RoleDto?> GetByIdAsync(Guid id, Guid callerTenantId, CancellationToken ct);
    Task<RoleDto> CreateAsync(Guid tenantId, CreateRoleRequest request, CancellationToken ct);
    Task UpdateAsync(Guid id, Guid callerTenantId, UpdateRoleRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, Guid callerTenantId, CancellationToken ct);
    Task AssignPermissionAsync(Guid roleId, string permissionCode, Guid tenantId, Guid? changedByUserId, string? changedByName, CancellationToken ct);
    Task RemovePermissionAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct);
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid roleId, Guid callerTenantId, CancellationToken ct);
}

using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.DTOs.Common;
using ERP.Identity.Application.DTOs.Permission;

namespace ERP.Identity.Application.Interfaces.Services;

public interface IUserService
{
    Task<UserDetailDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<PagedResponse<UserDetailDto>> ListByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct);
    Task UpdateProfileAsync(Guid id, UpdateProfileRequest request, CancellationToken ct);
    Task ChangePasswordAsync(Guid id, ChangePasswordRequest request, CancellationToken ct);
    Task LockAsync(Guid id, CancellationToken ct);
    Task UnlockAsync(Guid id, CancellationToken ct);
    Task DeactivateAsync(Guid id, CancellationToken ct);
    Task AssignRoleAsync(Guid userId, string roleCode, Guid tenantId, CancellationToken ct);
    Task RemoveRoleAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct);
    Task EnableTwoFactorAsync(Guid id, string method, string? phoneNumber, CancellationToken ct);
    Task DisableTwoFactorAsync(Guid id, CancellationToken ct);
}

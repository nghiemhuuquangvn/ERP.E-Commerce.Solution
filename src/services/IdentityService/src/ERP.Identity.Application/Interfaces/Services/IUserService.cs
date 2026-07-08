using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.DTOs.Common;
using ERP.Identity.Application.DTOs.Permission;

namespace ERP.Identity.Application.Interfaces.Services;

public interface IUserService
{
    /// <summary>Tenant-boundary-enforced — throws TenantIsolationException if the user
    /// isn't a member of callerTenantId.</summary>
    Task<UserDetailDto?> GetByIdAsync(Guid id, Guid callerTenantId, CancellationToken ct);
    Task<PagedResponse<UserDetailDto>> ListByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct);
    Task UpdateProfileAsync(Guid id, Guid callerTenantId, UpdateProfileRequest request, CancellationToken ct);
    Task ChangePasswordAsync(Guid id, ChangePasswordRequest request, CancellationToken ct);
    Task LockAsync(Guid id, Guid callerTenantId, CancellationToken ct);
    Task UnlockAsync(Guid id, Guid callerTenantId, CancellationToken ct);
    Task DeactivateAsync(Guid id, Guid callerTenantId, CancellationToken ct);
    Task AssignRoleAsync(Guid userId, string roleCode, Guid tenantId, CancellationToken ct);
    Task RemoveRoleAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct);
    Task EnableTwoFactorAsync(Guid id, string method, string? phoneNumber, CancellationToken ct);
    Task DisableTwoFactorAsync(Guid id, CancellationToken ct);

    /// <summary>Attaches an already-existing account (by email) to callerTenantId as a
    /// new AuthUserTenant membership — does not create new accounts. Idempotent:
    /// returns AlreadyMember=true, no-op, if the membership already exists.</summary>
    Task<InviteUserToTenantResponse> InviteToTenantAsync(Guid callerTenantId, InviteUserToTenantRequest request, CancellationToken ct);
}

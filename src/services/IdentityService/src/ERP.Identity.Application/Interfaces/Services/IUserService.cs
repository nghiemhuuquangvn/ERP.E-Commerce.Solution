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
    /// <summary>Refused (business rule) for the caller's own account and for the last administrator of the tenant.</summary>
    Task LockAsync(Guid id, Guid callerTenantId, Guid? callerUserId, CancellationToken ct);
    Task UnlockAsync(Guid id, Guid callerTenantId, CancellationToken ct);
    /// <summary>Refused (business rule) for the caller's own account and for the last administrator of the tenant.</summary>
    Task DeactivateAsync(Guid id, Guid callerTenantId, Guid? callerUserId, CancellationToken ct);
    /// <summary>Re-activates a deactivated member of the caller's tenant (R91: there was no way back).</summary>
    Task ActivateAsync(Guid id, Guid callerTenantId, CancellationToken ct);
    Task AssignRoleAsync(Guid userId, string roleCode, Guid tenantId, Guid? changedByUserId, string? changedByName, CancellationToken ct);
    Task RemoveRoleAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct);
    /// <summary>Changing 2FA re-asks for the current password (or, for an account that only signs in through an external
    /// provider, needs an access token issued within <c>Auth:ReauthenticationWindowMinutes</c>).</summary>
    Task EnableTwoFactorAsync(Guid id, string method, string? phoneNumber, string? currentPassword, DateTimeOffset? tokenIssuedAt, CancellationToken ct);
    Task DisableTwoFactorAsync(Guid id, string? currentPassword, DateTimeOffset? tokenIssuedAt, CancellationToken ct);

    /// <summary>Attaches an already-existing account (by email) to callerTenantId as a
    /// new AuthUserTenant membership — does not create new accounts. Idempotent:
    /// returns AlreadyMember=true, no-op, if the membership already exists.</summary>
    Task<InviteUserToTenantResponse> InviteToTenantAsync(Guid callerTenantId, InviteUserToTenantRequest request, CancellationToken ct);
}

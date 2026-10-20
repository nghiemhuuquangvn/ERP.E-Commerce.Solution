using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.DTOs.Common;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Localization;
using ERP.Shared.Infrastructure.Exceptions;
using Microsoft.Extensions.Localization;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ISecurityAuditService _securityAuditService;
        private readonly IPasswordPolicyService _passwordPolicyService;
        private readonly IStringLocalizer<AuthMessages> _localizer;
        private readonly ISessionService _sessionService;

        public UserService(
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            ISecurityAuditService securityAuditService,
            IPasswordPolicyService passwordPolicyService,
            IStringLocalizer<AuthMessages> localizer,
            ISessionService sessionService)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _securityAuditService = securityAuditService;
            _passwordPolicyService = passwordPolicyService;
            _localizer = localizer;
            _sessionService = sessionService;
        }

        public async Task<UserDetailDto?> GetByIdAsync(Guid id, Guid callerTenantId, CancellationToken ct)
        {
            // Tenant-scoped fetch — a user in another tenant collapses to the same
            // "not found" outcome as a user that doesn't exist at all (anti-enumeration).
            var user = await _unitOfWork.Users.GetByIdWithDetailsInTenantAsync(id, callerTenantId, ct);
            if (user == null)
                return null;

            var roles = user.UserRoles.Select(ur => ur.Role.RoleName).ToList();
            var roleAssignments = user.UserRoles
                .Select(ur => new RoleAssignmentDto(ur.RoleId, ur.Role.RoleCode, ur.Role.RoleName))
                .ToList();
            var permissions = user.UserRoles
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.PermissionCode)
                .Distinct()
                .ToList();

            return new UserDetailDto(
                user.Id,
                user.Email,
                user.UserName,
                user.FullName,
                user.IsActive,
                user.IsLocked,
                user.LastLoginAt,
                roles,
                permissions,
                roleAssignments
            );
        }

        public async Task<PagedResponse<UserDetailDto>> ListByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct)
        {
            var users = await _unitOfWork.Users.ListByTenantAsync(tenantId, page, pageSize, ct);
            var totalCount = await _unitOfWork.Users.CountByTenantAsync(tenantId, ct);

            var userDtos = users.Select(u =>
            {
                var roles = u.UserRoles.Select(ur => ur.Role.RoleName).ToList();
                var roleAssignments = u.UserRoles
                    .Select(ur => new RoleAssignmentDto(ur.RoleId, ur.Role.RoleCode, ur.Role.RoleName))
                    .ToList();
                var permissions = u.UserRoles
                    .SelectMany(ur => ur.Role.RolePermissions)
                    .Select(rp => rp.Permission.PermissionCode)
                    .Distinct()
                    .ToList();

                return new UserDetailDto(
                    u.Id,
                    u.Email,
                    u.UserName,
                    u.FullName,
                    u.IsActive,
                    u.IsLocked,
                    u.LastLoginAt,
                    roles,
                    permissions,
                    roleAssignments
                );
            }).ToList();

            return new PagedResponse<UserDetailDto>(userDtos, totalCount, page, pageSize);
        }

        public async Task UpdateProfileAsync(Guid id, Guid callerTenantId, UpdateProfileRequest request, CancellationToken ct)
        {
            var user = await _unitOfWork.Users.GetByIdInTenantAsync(id, callerTenantId, ct)
                ?? throw new EntityNotFoundException("User", id);

            user.UpdateProfile(request.FullName, request.UserName);
            await _unitOfWork.Users.UpdateAsync(user, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task ChangePasswordAsync(Guid id, ChangePasswordRequest request, CancellationToken ct)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id, ct);
            if (user == null)
                throw new InvalidOperationException($"User with id '{id}' not found");

            if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.CurrentPassword))
            {
                await _securityAuditService.LogAsync("PasswordChange", false, user.TenantId, id, user.Email, errorReason: "Current password incorrect", ct: ct);
                await _unitOfWork.SaveChangesAsync(ct);
                throw new InvalidOperationException("Current password is incorrect");
            }

            await _passwordPolicyService.ValidateNewPasswordAsync(user.TenantId, id, request.NewPassword, ct);

            var previousHash = user.PasswordHash;
            var newPasswordHash = _passwordHasher.HashPassword(request.NewPassword);
            user.ChangePassword(newPasswordHash);

            await _passwordPolicyService.RecordPasswordHistoryAsync(id, previousHash, ct);
            await _unitOfWork.Users.UpdateAsync(user, ct);

            // Changing the password signs the user out everywhere (refresh tokens + device
            // sessions); the current access token stays valid only until it expires.
            await _sessionService.RevokeAllForUserAsync(id, ct);
            await _securityAuditService.LogAsync("PasswordChange", true, user.TenantId, id, user.Email, ct: ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task LockAsync(Guid id, Guid callerTenantId, CancellationToken ct)
        {
            var user = await _unitOfWork.Users.GetByIdInTenantAsync(id, callerTenantId, ct)
                ?? throw new EntityNotFoundException("User", id);

            user.Lock();
            await _unitOfWork.Users.UpdateAsync(user, ct);
            // A locked account must stop working everywhere now, not when its tokens expire
            // (decision 2026-10-20): revoke refresh tokens/sessions and every issued token.
            await _sessionService.RevokeAllForUserAsync(id, ct);
            await _securityAuditService.LogAsync("AccountLocked", true, user.TenantId, id, user.Email, ct: ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task UnlockAsync(Guid id, Guid callerTenantId, CancellationToken ct)
        {
            var user = await _unitOfWork.Users.GetByIdInTenantAsync(id, callerTenantId, ct)
                ?? throw new EntityNotFoundException("User", id);

            user.Unlock();
            await _unitOfWork.Users.UpdateAsync(user, ct);
            await _securityAuditService.LogAsync("AccountUnlocked", true, user.TenantId, id, user.Email, ct: ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task DeactivateAsync(Guid id, Guid callerTenantId, CancellationToken ct)
        {
            var user = await _unitOfWork.Users.GetByIdInTenantAsync(id, callerTenantId, ct)
                ?? throw new EntityNotFoundException("User", id);

            user.Deactivate();
            await _unitOfWork.Users.UpdateAsync(user, ct);

            // Deactivation must end existing sessions too — otherwise the user's refresh
            // token keeps working until it naturally expires.
            await _sessionService.RevokeAllForUserAsync(user.Id, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task AssignRoleAsync(Guid userId, string roleCode, Guid tenantId, Guid? changedByUserId, string? changedByName, CancellationToken ct)
        {
            // Tenant-scoped lookup: an unscoped GetByIdAsync let a tenant admin grant one of
            // their tenant's roles to a user of a DIFFERENT tenant just by knowing the Id.
            var user = await _unitOfWork.Users.GetByIdInTenantAsync(userId, tenantId, ct)
                ?? throw new EntityNotFoundException("User", userId);

            var role = await _unitOfWork.Roles.GetByCodeAsync(roleCode, tenantId, ct);
            if (role == null)
                throw new InvalidOperationException($"Role '{roleCode}' not found for tenant");

            user.AssignRole(role, tenantId, changedByUserId, changedByName);
            await _unitOfWork.Users.UpdateAsync(user, ct);
            _unitOfWork.Revocations.UsersChanged(userId); // permissions changed: refresh
            await _securityAuditService.LogAsync("RoleAssigned", true, tenantId, userId, user.Email, details: $"Role={roleCode}", ct: ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task RemoveRoleAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct)
        {
            var user = await _unitOfWork.Users.GetByIdInTenantAsync(userId, tenantId, ct)
                ?? throw new EntityNotFoundException("User", userId);

            var userRole = user.UserRoles.FirstOrDefault(ur => ur.RoleId == roleId && ur.TenantId == tenantId);
            if (userRole == null)
                throw new InvalidOperationException($"User doesn't have this role");

            // Previously a no-op: this method removed nothing and just called
            // SaveChangesAsync, so DELETE /api/users/{id}/roles/{roleId} always
            // returned 200 OK while leaving the role assigned. A direct repository
            // delete (not a collection-remove-then-hope-EF-notices) actually persists it.
            await _unitOfWork.Users.RemoveRoleAsync(userId, roleId, tenantId, ct);
            _unitOfWork.Revocations.UsersChanged(userId); // permissions changed: refresh
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task<InviteUserToTenantResponse> InviteToTenantAsync(Guid callerTenantId, InviteUserToTenantRequest request, CancellationToken ct)
        {
            // Email is only unique per-(Email, TenantId) since migration 014, so the same
            // address can legitimately belong to more than one unrelated account across
            // tenants — an unordered "pick one" (the old GetByEmailGloballyAsync-only
            // lookup) could silently attach a stranger's account to this tenant instead of
            // the intended person. Fail loudly instead of guessing.
            var matches = await _unitOfWork.Users.FindAllByEmailGloballyAsync(request.Email, ct);
            if (matches.Count == 0)
                throw new KeyNotFoundException($"No account found for email '{request.Email}'.");
            if (matches.Count > 1)
                throw new BusinessRuleException("AmbiguousInviteEmail", _localizer["Invite_AmbiguousEmail", request.Email].Value);

            var user = matches[0];

            if (await _unitOfWork.Users.IsInTenantAsync(user.Id, callerTenantId, ct))
                return new InviteUserToTenantResponse(user.Id, callerTenantId, AlreadyMember: true);

            var tenant = await _unitOfWork.Tenants.GetByIdAsync(callerTenantId, ct)
                ?? throw new InvalidOperationException("Tenant not found");

            user.AssignToTenant(tenant);

            if (!string.IsNullOrWhiteSpace(request.RoleCode))
            {
                var role = await _unitOfWork.Roles.GetByCodeAsync(request.RoleCode, callerTenantId, ct)
                    ?? throw new InvalidOperationException($"Role '{request.RoleCode}' not found for tenant");
                user.AssignRole(role, callerTenantId);
            }

            await _unitOfWork.Users.UpdateAsync(user, ct);
            await _securityAuditService.LogAsync("UserInvitedToTenant", true, callerTenantId, user.Id, user.Email, ct: ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new InviteUserToTenantResponse(user.Id, callerTenantId, AlreadyMember: false);
        }

        public async Task EnableTwoFactorAsync(Guid id, string method, string? phoneNumber, CancellationToken ct)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id, ct);
            if (user == null)
                throw new InvalidOperationException($"User with id '{id}' not found");

            user.EnableTwoFactor(method, phoneNumber);
            await _unitOfWork.Users.UpdateAsync(user, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task DisableTwoFactorAsync(Guid id, CancellationToken ct)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id, ct);
            if (user == null)
                throw new InvalidOperationException($"User with id '{id}' not found");

            user.DisableTwoFactor();
            await _unitOfWork.Users.UpdateAsync(user, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }
}

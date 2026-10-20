using ERP.Identity.Application.DTOs.Permission;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Security;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Services
{
    public class RoleService : IRoleService
    {
        private readonly IUnitOfWork _unitOfWork;

        public RoleService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IReadOnlyList<RoleDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct)
        {
            var roles = await _unitOfWork.Roles.ListByTenantAsync(tenantId, ct);
            return roles.Select(r => new RoleDto(r.Id, r.RoleCode, r.RoleName, r.IsActive)).ToList();
        }

        public async Task<RoleDto?> GetByIdAsync(Guid id, Guid callerTenantId, CancellationToken ct)
        {
            var role = await _unitOfWork.Roles.GetByIdInTenantAsync(id, callerTenantId, ct);
            if (role == null)
                return null;

            return new RoleDto(role.Id, role.RoleCode, role.RoleName, role.IsActive);
        }

        public async Task<RoleDto> CreateAsync(Guid tenantId, CreateRoleRequest request, CancellationToken ct)
        {
            // Platform role codes (SuperAdmin, …) can't be recreated by anyone — a tenant
            // admin with a role named "SuperAdmin" used to bypass every permission check.
            if (PlatformRoles.IsReserved(request.RoleCode))
                throw new BusinessRuleException("ReservedRoleCode", $"Role code '{request.RoleCode}' is reserved.");

            var existingRole = await _unitOfWork.Roles.GetByCodeAsync(request.RoleCode, tenantId, ct);
            if (existingRole != null)
                throw new InvalidOperationException($"Role '{request.RoleCode}' already exists for this tenant");

            var role = Role.Create(request.RoleCode, request.RoleName, tenantId);
            await _unitOfWork.Roles.AddAsync(role, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new RoleDto(role.Id, role.RoleCode, role.RoleName, role.IsActive);
        }

        public async Task UpdateAsync(Guid id, Guid callerTenantId, UpdateRoleRequest request, CancellationToken ct)
        {
            var role = await _unitOfWork.Roles.GetByIdInTenantAsync(id, callerTenantId, ct)
                ?? throw new EntityNotFoundException("Role", id);

            role.RoleName = request.RoleName;
            await _unitOfWork.Roles.UpdateAsync(role, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(Guid id, Guid callerTenantId, CancellationToken ct)
        {
            var role = await _unitOfWork.Roles.GetByIdInTenantAsync(id, callerTenantId, ct)
                ?? throw new EntityNotFoundException("Role", id);

            // Resolve holders before the delete cascades their UserRoles away.
            _unitOfWork.Revocations.UsersChanged((await _unitOfWork.Users.GetUserIdsByRoleAsync(id, callerTenantId, ct)).ToArray());
            await _unitOfWork.Roles.DeleteAsync(role, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task AssignPermissionAsync(Guid roleId, string permissionCode, Guid tenantId, Guid? changedByUserId, string? changedByName, CancellationToken ct)
        {
            // Previously fetched via the un-scoped GetByIdAsync and never actually
            // checked role.TenantId == tenantId — a caller could attach permissions to
            // another tenant's role by GUID despite the controller already extracting
            // and passing the caller's own tenantId down here.
            var role = await _unitOfWork.Roles.GetByIdInTenantAsync(roleId, tenantId, ct)
                ?? throw new EntityNotFoundException("Role", roleId);

            var permissions = await _unitOfWork.Permissions.ListAllAsync(ct);
            var permission = permissions.FirstOrDefault(p => p.PermissionCode == permissionCode);
            if (permission == null)
                throw new InvalidOperationException($"Permission '{permissionCode}' not found");

            // Anti-escalation: no platform permissions outside the platform tenant, and no
            // granting what you don't hold. A tenant admin with identity:roles:manage could
            // previously add settings:system:manage / identity:tenants:* to their own role.
            var denial = await EffectivePermissions.WhyNotGrantableAsync(_unitOfWork, permission.PermissionCode, tenantId, changedByUserId, ct);
            if (denial != null)
                throw new BusinessRuleException("PermissionNotGrantable", denial);

            await _unitOfWork.Permissions.AssignToRoleAsync(roleId, permission.Id, tenantId, ct);

            // Every user holding this role is affected by the grant — resolved here since
            // Identity already owns the UserRoles join, keeping the published domain event
            // fully self-describing for NotificationService's bell notification.
            var affectedUserIds = await _unitOfWork.Users.GetUserIdsByRoleAsync(roleId, tenantId, ct);
            _unitOfWork.Revocations.UsersChanged(affectedUserIds.ToArray()); // their tokens lack the new grant
            role.NotifyPermissionGranted(permission.Id, permission.PermissionCode, affectedUserIds, changedByUserId, changedByName);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task RemovePermissionAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct)
        {
            var role = await _unitOfWork.Roles.GetByIdInTenantAsync(roleId, tenantId, ct)
                ?? throw new EntityNotFoundException("Role", roleId);

            await _unitOfWork.Permissions.RemoveFromRoleAsync(roleId, permissionId, tenantId, ct);
            // Holders of the role must not keep the removed permission until their token expires.
            _unitOfWork.Revocations.UsersChanged((await _unitOfWork.Users.GetUserIdsByRoleAsync(roleId, tenantId, ct)).ToArray());
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid roleId, Guid callerTenantId, CancellationToken ct)
        {
            _ = await _unitOfWork.Roles.GetByIdInTenantAsync(roleId, callerTenantId, ct)
                ?? throw new EntityNotFoundException("Role", roleId);

            return await _unitOfWork.Permissions.GetPermissionsForRoleAsync(roleId, ct);
        }
    }
}

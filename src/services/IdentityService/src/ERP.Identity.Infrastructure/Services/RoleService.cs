using ERP.Identity.Application.DTOs.Permission;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
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

        public async Task<RoleDto?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var role = await _unitOfWork.Roles.GetByIdAsync(id, ct);
            if (role == null)
                return null;

            return new RoleDto(role.Id, role.RoleCode, role.RoleName, role.IsActive);
        }

        public async Task<RoleDto> CreateAsync(Guid tenantId, CreateRoleRequest request, CancellationToken ct)
        {
            var existingRole = await _unitOfWork.Roles.GetByCodeAsync(request.RoleCode, tenantId, ct);
            if (existingRole != null)
                throw new InvalidOperationException($"Role '{request.RoleCode}' already exists for this tenant");

            var role = Role.Create(request.RoleCode, request.RoleName, tenantId);
            await _unitOfWork.Roles.AddAsync(role, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new RoleDto(role.Id, role.RoleCode, role.RoleName, role.IsActive);
        }

        public async Task UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct)
        {
            var role = await _unitOfWork.Roles.GetByIdAsync(id, ct);
            if (role == null)
                throw new InvalidOperationException($"Role with id '{id}' not found");

            role.RoleName = request.RoleName;
            await _unitOfWork.Roles.UpdateAsync(role, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            var role = await _unitOfWork.Roles.GetByIdAsync(id, ct);
            if (role == null)
                throw new InvalidOperationException($"Role with id '{id}' not found");

            await _unitOfWork.Roles.DeleteAsync(role, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task AssignPermissionAsync(Guid roleId, string permissionCode, Guid tenantId, CancellationToken ct)
        {
            var role = await _unitOfWork.Roles.GetByIdAsync(roleId, ct);
            if (role == null)
                throw new InvalidOperationException($"Role with id '{roleId}' not found");

            var permissions = await _unitOfWork.Permissions.ListAllAsync(ct);
            var permission = permissions.FirstOrDefault(p => p.PermissionCode == permissionCode);
            if (permission == null)
                throw new InvalidOperationException($"Permission '{permissionCode}' not found");

            await _unitOfWork.Permissions.AssignToRoleAsync(roleId, permission.Id, tenantId, ct);
            role.NotifyPermissionGranted(permission.Id, permission.PermissionCode);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task RemovePermissionAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct)
        {
            var role = await _unitOfWork.Roles.GetByIdAsync(roleId, ct);
            if (role == null)
                throw new InvalidOperationException($"Role with id '{roleId}' not found");

            await _unitOfWork.Permissions.RemoveFromRoleAsync(roleId, permissionId, tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid roleId, CancellationToken ct)
        {
            return await _unitOfWork.Permissions.GetPermissionsForRoleAsync(roleId, ct);
        }
    }
}

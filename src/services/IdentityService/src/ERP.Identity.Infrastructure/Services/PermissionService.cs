using ERP.Identity.Application.DTOs.Permission;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private readonly IPermissionRepository _permissionRepository;
    public PermissionService(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, IEnumerable<string> permissionCodes)
    {
        return await _permissionRepository.HasPermissionAsync(userId, tenantId, permissionCodes);
    }

    public async Task<IReadOnlyList<PermissionDto>> ListAllAsync(CancellationToken ct)
    {
        var permissions = await _permissionRepository.ListAllAsync(ct);
        return permissions.Select(p => new PermissionDto(p.Id, p.PermissionCode, p.PermissionName, p.Module)).ToList();
    }

    public async Task<PermissionDto> CreateAsync(CreatePermissionRequest request, CancellationToken ct)
    {
        var existingPermission = await _permissionRepository.ExistsAsync(request.PermissionCode, ct);
        if (existingPermission)
            throw new InvalidOperationException($"Permission '{request.PermissionCode}' already exists");

        var permission = Permission.Create(request.PermissionCode, request.PermissionName, request.Module);
        await _permissionRepository.AddAsync(permission, ct);

        return new PermissionDto(permission.Id, permission.PermissionCode, permission.PermissionName, permission.Module);
    }

    public async Task UpdateAsync(Guid id, UpdatePermissionRequest request, CancellationToken ct)
    {
        var permission = await _permissionRepository.GetByIdAsync(id, ct);
        if (permission == null)
            throw new InvalidOperationException($"Permission with id '{id}' not found");

        if (!string.IsNullOrWhiteSpace(request.PermissionName))
            permission.PermissionName = request.PermissionName;
        if (!string.IsNullOrWhiteSpace(request.Module))
            permission.Module = request.Module;

        await _permissionRepository.UpdateAsync(permission, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var permission = await _permissionRepository.GetByIdAsync(id, ct);
        if (permission == null)
            throw new InvalidOperationException($"Permission with id '{id}' not found");

        await _permissionRepository.DeleteAsync(permission, ct);
    }
}
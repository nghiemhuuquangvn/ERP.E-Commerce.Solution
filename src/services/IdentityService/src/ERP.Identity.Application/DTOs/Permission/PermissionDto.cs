namespace ERP.Identity.Application.DTOs.Permission;

public record PermissionDto(Guid Id, string PermissionCode, string? PermissionName, string? Module);

public record CreatePermissionRequest(string PermissionCode, string PermissionName, string? Module = null);

public record UpdatePermissionRequest(string? PermissionName = null, string? Module = null);

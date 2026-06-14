namespace ERP.Identity.Application.DTOs.Permission;

public record RoleDto(Guid Id, string RoleCode, string RoleName, bool IsActive);

public record CreateRoleRequest(string RoleCode, string RoleName);

public record UpdateRoleRequest(string RoleName);

public record AssignRoleRequest(Guid UserId, string RoleCode);

public record AssignPermissionToRoleRequest(string PermissionCode);

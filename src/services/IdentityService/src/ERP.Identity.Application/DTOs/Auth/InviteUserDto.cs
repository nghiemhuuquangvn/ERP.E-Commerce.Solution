namespace ERP.Identity.Application.DTOs.Auth;

public record InviteUserToTenantRequest(string Email, string? RoleCode = null);

public record InviteUserToTenantResponse(Guid UserId, Guid TenantId, bool AlreadyMember);

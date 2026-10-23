namespace ERP.Identity.Application.DTOs.Auth;

public record UserDto(Guid Id, string Email, List<string>? Roles, List<string>? Permissions);

public record UserDetailDto(
    Guid Id,
    string Email,
    string? UserName,
    string? FullName,
    bool IsActive,
    bool IsLocked,
    DateTime? LastLoginAt,
    List<string> Roles,
    List<string> Permissions,
    List<RoleAssignmentDto> RoleAssignments
);

// RoleId is needed alongside RoleCode/RoleName so the frontend can call
// DELETE /api/users/{id}/roles/{roleId} — the plain Roles: List<string> above only
// has display names, not enough to target a specific assignment for removal.
public record RoleAssignmentDto(Guid RoleId, string RoleCode, string RoleName);

public record UpdateProfileRequest(string? FullName, string? UserName);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>CurrentPassword re-confirms the caller (ROADMAP P0-8). Accounts that only ever signed in through an external
/// provider have no password of their own; they may omit it while their access token is still fresh.</summary>
public record EnableTwoFactorRequest(string Method, string? PhoneNumber, string? CurrentPassword = null);

public record DisableTwoFactorRequest(string? CurrentPassword = null);
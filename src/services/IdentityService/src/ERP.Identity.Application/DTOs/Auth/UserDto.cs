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
    List<string> Permissions
);

public record UpdateProfileRequest(string? FullName, string? UserName);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
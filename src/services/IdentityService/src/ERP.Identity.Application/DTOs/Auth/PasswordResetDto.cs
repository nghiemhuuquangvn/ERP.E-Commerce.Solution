namespace ERP.Identity.Application.DTOs.Auth;

public record ForgotPasswordRequest(string Email, string TenantCode);

public record ForgotPasswordResponse(bool Succeeded, string? ResetToken = null, IEnumerable<string>? Errors = null);

public record ResetPasswordRequest(string Token, string NewPassword);

public record ResetPasswordResponse(bool Succeeded = true, IEnumerable<string>? Errors = null);

public record ConfirmEmailRequest(string UserId, string Token);

public record ConfirmEmailResponse(bool Succeeded = true, IEnumerable<string>? Errors = null);

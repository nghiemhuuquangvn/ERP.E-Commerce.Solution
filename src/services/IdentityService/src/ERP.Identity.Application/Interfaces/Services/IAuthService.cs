using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ERP.Identity.Application.DTOs.Auth;
using static ERP.Identity.Application.DTOs.Auth.ExternalLoginDto;
using static ERP.Identity.Application.DTOs.Auth.LoginDto;
using static ERP.Identity.Application.DTOs.Auth.RefreshTokenDto;
using static ERP.Identity.Application.DTOs.Auth.RegisterDto;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request, string tenantCode, string? ipAddress, string? userAgent, CancellationToken ct);
        Task<LoginResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

        /// <summary>Self-service "create my own tenant" — provisions a brand-new tenant, seeds
        /// it a default self-administration Admin role, creates its first (Admin) user, and
        /// logs them in immediately. See TenantSignupDto for when to use this vs RegisterAsync
        /// vs TenantsController.Create.</summary>
        Task<LoginResponse> SignupTenantAsync(TenantSignupDto.TenantSignupRequest request, CancellationToken ct = default);

        Task<LoginResponse> ExternalLoginAsync(ExternalLoginRequest request, string tenantCode, CancellationToken ct = default);

        Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);

        Task LogoutAsync(Guid userId, string refreshToken, string? jti, DateTime? tokenExpiresAt, string? ipAddress, CancellationToken ct = default);

        Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct);

        Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);

        Task<ConfirmEmailResponse> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct);

        Task<LoginResponse> VerifyTwoFactorAsync(VerifyTwoFactorRequest request, string? ipAddress, string? userAgent, CancellationToken ct);

        Task<LoginResponse> ResendTwoFactorAsync(ResendTwoFactorRequest request, CancellationToken ct);

        Task<TwitterAuthorizeUrlResponse> GetTwitterAuthorizeUrlAsync(string tenantCode, CancellationToken ct);

        Task<LoginResponse> TwitterCallbackAsync(TwitterCallbackRequest request, CancellationToken ct);

        /// <summary>
        /// Credential check for the interactive OIDC /connect/login form, with the same
        /// brute-force counter, per-account lockout, IsActive check and audit trail as the
        /// REST login. Returns the authenticated user's Id, or null on any failure.
        /// </summary>
        Task<Guid?> VerifyInteractiveCredentialsAsync(string email, string password, string? ipAddress, string? userAgent, CancellationToken ct);

        /// <summary>
        /// Returns why this user may not complete an interactive OIDC sign-in into this
        /// tenant (deactivated/locked user, inactive tenant, or 2FA required — the OIDC flow
        /// has no 2FA step, so it fails closed), or null when allowed.
        /// </summary>
        Task<string?> GetInteractiveLoginDenialAsync(Guid userId, Guid tenantId, CancellationToken ct);
    }
}

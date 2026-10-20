using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ERP.Identity.Api.Authorization;
using ERP.Shared.Authorization;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.Services;
using LoginRequest = ERP.Identity.Application.DTOs.Auth.LoginDto.LoginRequest;
using RegisterRequest = ERP.Identity.Application.DTOs.Auth.RegisterDto.RegisterRequest;
using TenantSignupRequest = ERP.Identity.Application.DTOs.Auth.TenantSignupDto.TenantSignupRequest;
using RefreshTokenRequest = ERP.Identity.Application.DTOs.Auth.RefreshTokenDto.RefreshTokenRequest;
using ForgotPasswordRequestDto = ERP.Identity.Application.DTOs.Auth.ForgotPasswordRequest;
using ResetPasswordRequestDto = ERP.Identity.Application.DTOs.Auth.ResetPasswordRequest;
using ExternalLoginRequest = ERP.Identity.Application.DTOs.Auth.ExternalLoginDto.ExternalLoginRequest;

namespace ERP.Identity.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ISettingsService _settingsService;

        public AuthController(IAuthService authService, ISettingsService settingsService)
        {
            _authService = authService;
            _settingsService = settingsService;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-sensitive")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken = default)
        {
            // The tenant is the one of the host the request came in on (set by the Gateway),
            // never the client-supplied body value.
            if (!TryGetTenantCode(out var tenantCode, out var error)) return error!;
            var result = await _authService.RegisterAsync(request with { TenantCode = tenantCode! }, cancellationToken);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result); // LoginResponse: { accessToken, refreshToken, user, succeeded }
        }

        /// <summary>
        /// Self-service "create my own tenant" — provisions a brand-new tenant + its first
        /// Admin user + a default self-administration role, in one anonymous call. The only
        /// way into a fresh deployment beyond the SQL-seeded SYSTEM tenant; see
        /// docs/business/Identity/MultiTenantLogin.md and TenantSignupDto's doc comment for
        /// how this differs from Register (needs an existing tenant) and
        /// TenantsController.Create (admin-only, no user/roles seeded).
        /// </summary>
        [HttpPost("tenant-signup")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-sensitive")]
        public async Task<IActionResult> SignupTenant([FromBody] TenantSignupRequest request, CancellationToken cancellationToken = default)
        {
            // New tenants are created only from the platform host — a tenant's own domain
            // must never be able to spawn other tenants.
            if (!TryGetTenantCode(out var tenantCode, out var error)) return error!;
            if (!string.Equals(tenantCode, PlatformTenant.Code, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var result = await _authService.SignupTenantAsync(request, cancellationToken);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result); // LoginResponse: { accessToken, refreshToken, user, succeeded }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-sensitive")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
        {
            if (!TryGetTenantCode(out var tenantCode, out var error)) return error!;
            var result = await _authService.LoginAsync(request, tenantCode!, GetClientIp(), Request.Headers.UserAgent.ToString(), cancellationToken);
            if (!result.Succeeded) return Unauthorized(result.Errors);
            return Ok(result);
        }

        /// <summary>
        /// Client IP for brute-force counters and audit logging. Never reads X-Forwarded-For
        /// directly (client-controlled) — UseForwardedHeaders in Program.cs has already
        /// resolved RemoteIpAddress from it when, and only when, the sender is a trusted proxy.
        /// </summary>
        private string GetClientIp() =>
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        /// <summary>
        /// Reads the tenant code the Gateway resolved from the request's subdomain
        /// (see GatewayAuthorizationMiddleware.ResolveTenantCodeFromHost) — replaces the old
        /// manual "Tenant Code" login field. Missing header means the request didn't come
        /// through a recognizable tenant subdomain (e.g. hit the bare base domain directly),
        /// which is a hard error here rather than a silent fallback to some default tenant.
        /// </summary>
        private bool TryGetTenantCode(out string? tenantCode, out IActionResult? error)
        {
            tenantCode = Request.Headers["X-Tenant-Code"].ToString();
            if (string.IsNullOrWhiteSpace(tenantCode))
            {
                error = BadRequest(new { message = "Unable to resolve tenant from request host." });
                return false;
            }
            error = null;
            return true;
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken = default)
        {
            // Bound to the host's tenant: a refresh token from tenant A is useless on tenant B's
            // domain (AuthService rejects the mismatch). Ignores any client-supplied TenantId.
            if (!Guid.TryParse(Request.Headers["X-Tenant-Id"].ToString(), out var hostTenantId))
                return BadRequest(new { message = "Unable to resolve tenant from request host." });
            var refreshTokenRequest = new RefreshTokenRequest(request.RefreshToken, hostTenantId, GetClientIp());

            var result = await _authService.RefreshTokenAsync(refreshTokenRequest, cancellationToken);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result);
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken = default)
        {
            var callerUserId = this.GetCallerUserId();
            if (callerUserId is null)
                return Unauthorized();

            var jti = User.FindFirst("jti")?.Value;
            DateTime? tokenExpiresAt = long.TryParse(User.FindFirst("exp")?.Value, out var expUnix)
                ? DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime
                : null;

            await _authService.LogoutAsync(callerUserId.Value, request.RefreshToken, jti, tokenExpiresAt, GetClientIp(), cancellationToken);
            return Ok(new { message = "Logged out successfully" });
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-sensitive")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request, CancellationToken cancellationToken = default)
        {
            if (!TryGetTenantCode(out var tenantCode, out var error)) return error!;
            var result = await _authService.ForgotPasswordAsync(request with { TenantCode = tenantCode! }, cancellationToken);
            return Ok(result);
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-sensitive")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.ResetPasswordAsync(request, cancellationToken);
            return Ok(result);
        }

        [HttpPost("confirm-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.ConfirmEmailAsync(request, cancellationToken);
            return Ok(result);
        }

        [HttpPost("external-login")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-sensitive")]
        public async Task<IActionResult> ExternalLogin([FromBody] ExternalLoginRequest request, CancellationToken cancellationToken = default)
        {
            if (!TryGetTenantCode(out var tenantCode, out var error)) return error!;
            var result = await _authService.ExternalLoginAsync(request, tenantCode!, cancellationToken);
            if (!result.Succeeded) return Unauthorized(result.Errors);
            return Ok(result);
        }

        [HttpGet("external-login/twitter/authorize-url")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTwitterAuthorizeUrl(CancellationToken cancellationToken = default)
        {
            if (!TryGetTenantCode(out var tenantCode, out var error)) return error!;
            var result = await _authService.GetTwitterAuthorizeUrlAsync(tenantCode!, cancellationToken);
            return Ok(result);
        }

        [HttpPost("external-login/twitter/callback")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-sensitive")]
        public async Task<IActionResult> TwitterCallback([FromBody] TwitterCallbackRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.TwitterCallbackAsync(request, cancellationToken);
            if (!result.Succeeded) return Unauthorized(result.Errors);
            return Ok(result);
        }

        [HttpPost("2fa/verify")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-2fa-verify")]
        public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.VerifyTwoFactorAsync(request, GetClientIp(), Request.Headers.UserAgent.ToString(), cancellationToken);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result);
        }

        [HttpPost("2fa/resend")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-sensitive")]
        public async Task<IActionResult> ResendTwoFactor([FromBody] ResendTwoFactorRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.ResendTwoFactorAsync(request, cancellationToken);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result);
        }

        [HttpGet("login-options")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLoginOptions(CancellationToken cancellationToken = default)
        {
            if (!TryGetTenantCode(out var tenantCode, out var error)) return error!;
            var result = await _settingsService.GetPublicOptionsAsync(tenantCode!, cancellationToken);
            return Ok(result);
        }
    }
}